namespace TUIKit.Content
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>
    /// Parses unified diff text (from git, a code review API, a patch file, or a server) into diff lines and files.
    /// The extent of each hunk comes from the old and new counts in its <c>@@</c> header, so a body line whose
    /// content starts with <c>--</c> or <c>++</c> is never misread as a file header. Plain text, a truncated hunk,
    /// and combined diffs render literally; the parser never throws on content. Stateless and thread-safe.
    /// </summary>
    internal static class UnifiedDiffParser
    {
        private static readonly string[] _MetaPrefixes =
        {
            "index ", "new file mode", "deleted file mode", "old mode", "new mode", "similarity index",
            "dissimilarity index", "rename from", "rename to", "copy from", "copy to", "Binary files ", "GIT binary patch",
        };

        internal static void Parse(string diff, List<DiffLine> lines, List<DiffFile> files)
        {
            string[] raw = diff.Replace("\r\n", "\n").Split('\n');
            int count = raw.Length;
            if (count > 0 && raw[count - 1].Length == 0)
                count--;

            bool inHunk = false;
            int oldRemaining = 0;
            int newRemaining = 0;
            int oldLine = 0;
            int newLine = 0;
            DiffFile? current = null;
            for (int r = 0; r < count; r++)
            {
                string line = raw[r];
                if (inHunk)
                {
                    if (line.StartsWith("\\", StringComparison.Ordinal))
                    {
                        lines.Add(new DiffLine(DiffLineKind.Meta, line));
                        continue;
                    }

                    if (line.StartsWith("+", StringComparison.Ordinal) && newRemaining > 0)
                    {
                        lines.Add(new DiffLine(DiffLineKind.Added, line.Substring(1), 0, newLine++));
                        newRemaining--;
                    }
                    else if (line.StartsWith("-", StringComparison.Ordinal) && oldRemaining > 0)
                    {
                        lines.Add(new DiffLine(DiffLineKind.Removed, line.Substring(1), oldLine++, 0));
                        oldRemaining--;
                    }
                    else if ((line.StartsWith(" ", StringComparison.Ordinal) || line.Length == 0) && oldRemaining > 0 && newRemaining > 0)
                    {
                        lines.Add(new DiffLine(DiffLineKind.Context, line.Length == 0 ? string.Empty : line.Substring(1), oldLine++, newLine++));
                        oldRemaining--;
                        newRemaining--;
                    }
                    else
                    {
                        // The hunk ended early (truncated input or a line with no valid prefix): scan for headers again.
                        inHunk = false;
                    }

                    if (inHunk)
                    {
                        if (oldRemaining <= 0 && newRemaining <= 0)
                            inHunk = false;
                        continue;
                    }
                }

                if (line.StartsWith("diff --git ", StringComparison.Ordinal))
                {
                    current = new DiffFile(GitPathUnquoter.GitLinePath(line.Substring(11)), lines.Count);
                    files.Add(current);
                    lines.Add(new DiffLine(DiffLineKind.FileHeader, line));
                }
                else if (line.StartsWith("diff --cc ", StringComparison.Ordinal) || line.StartsWith("diff --combined ", StringComparison.Ordinal) || line.StartsWith("@@@", StringComparison.Ordinal))
                {
                    lines.Add(new DiffLine(DiffLineKind.Context, line));
                }
                else if (line.StartsWith("--- ", StringComparison.Ordinal) && r + 1 < count && raw[r + 1].StartsWith("+++ ", StringComparison.Ordinal))
                {
                    string oldPath = GitPathUnquoter.HeaderPath(line.Substring(4));
                    if (current == null || current.HasHeaderPaths)
                    {
                        current = new DiffFile(oldPath, lines.Count);
                        files.Add(current);
                    }

                    current.HasHeaderPaths = true;
                    current.OldPath = oldPath;
                    lines.Add(new DiffLine(DiffLineKind.FileHeader, line));
                }
                else if (line.StartsWith("+++ ", StringComparison.Ordinal) && current != null && current.HasHeaderPaths && lines.Count > 0 && lines[lines.Count - 1].Kind == DiffLineKind.FileHeader)
                {
                    string newPath = GitPathUnquoter.HeaderPath(line.Substring(4));
                    current.Path = newPath == "/dev/null" ? (current.OldPath ?? newPath) : newPath;
                    lines.Add(new DiffLine(DiffLineKind.FileHeader, line));
                }
                else if (line.StartsWith("@@ -", StringComparison.Ordinal) && TryParseHunk(line, out int oldStart, out int oldCount, out int newStart, out int newCount))
                {
                    lines.Add(new DiffLine(DiffLineKind.HunkHeader, line));
                    oldLine = oldStart;
                    newLine = newStart;
                    oldRemaining = oldCount;
                    newRemaining = newCount;
                    inHunk = oldCount > 0 || newCount > 0;
                }
                else if (IsMeta(line))
                {
                    lines.Add(new DiffLine(DiffLineKind.Meta, line));
                }
                else
                {
                    lines.Add(new DiffLine(DiffLineKind.Context, line));
                }
            }
        }

        internal static bool TryParseHunk(string line, out int oldStart, out int oldCount, out int newStart, out int newCount)
        {
            oldStart = oldCount = newStart = newCount = 0;
            int end = line.IndexOf(" @@", 4, StringComparison.Ordinal);
            if (end < 0)
                return false;

            string[] parts = line.Substring(4, end - 4).Split(' ');
            if (parts.Length != 2 || !parts[1].StartsWith("+", StringComparison.Ordinal))
                return false;

            return TryRange(parts[0], out oldStart, out oldCount) && TryRange(parts[1].Substring(1), out newStart, out newCount);
        }

        private static bool TryRange(string text, out int start, out int count)
        {
            count = 1;
            int comma = text.IndexOf(',');
            string first = comma >= 0 ? text.Substring(0, comma) : text;
            if (!int.TryParse(first, NumberStyles.None, CultureInfo.InvariantCulture, out start))
                return false;
            return comma < 0 || int.TryParse(text.Substring(comma + 1), NumberStyles.None, CultureInfo.InvariantCulture, out count);
        }

        private static bool IsMeta(string line)
        {
            if (line.StartsWith("\\ ", StringComparison.Ordinal))
                return true;

            foreach (string prefix in _MetaPrefixes)
            {
                if (line.StartsWith(prefix, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}

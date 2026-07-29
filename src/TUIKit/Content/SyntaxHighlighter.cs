namespace TUIKit.Content
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using TUIKit;

    /// <summary>
    /// A lightweight, line-oriented syntax highlighter. It recognizes strings, comments, numbers, and
    /// language keywords and maps them to colors, producing <see cref="StyledText"/> that can be
    /// written to a pane or shown inside a diff. Supported languages: <c>csharp</c>, <c>json</c>,
    /// <c>javascript</c>/<c>js</c>, <c>python</c>; anything else uses generic string/number/comment
    /// rules. Highlighting is per line, so multi-line strings and block comments are not tracked
    /// across lines. All members are thread-safe.
    /// </summary>
    public static class SyntaxHighlighter
    {
        private static readonly CellStyle _Keyword = CellStyle.Default.WithForeground(Color.FromPalette(5));
        private static readonly CellStyle _String = CellStyle.Default.WithForeground(Color.FromPalette(2));
        private static readonly CellStyle _Number = CellStyle.Default.WithForeground(Color.FromPalette(3));
        private static readonly CellStyle _Comment = CellStyle.Default.WithForeground(Color.FromPalette(8)).WithAttribute(CellAttributes.Dim, true);

        private static readonly HashSet<string> _CSharp = Keywords("public private protected internal class struct interface enum void int long double float bool string char var new return if else for foreach while do switch case break continue using namespace static readonly const async await this base null true false throw try catch finally get set override virtual abstract sealed in out ref params is as default typeof nameof");
        private static readonly HashSet<string> _Js = Keywords("function var let const return if else for while do new class extends this null true false undefined async await import export from default typeof instanceof of in try catch finally throw switch case break continue yield");
        private static readonly HashSet<string> _Python = Keywords("def class return if elif else for while import from as None True False and or not in is lambda try except finally with pass break continue raise yield global nonlocal assert del self");
        private static readonly HashSet<string> _Json = Keywords("true false null");

        /// <summary>
        /// Highlights a whole block of code, returning one styled line per source line.
        /// </summary>
        /// <param name="code">The source code. Must not be null.</param>
        /// <param name="language">The language identifier. Must not be null.</param>
        /// <returns>The highlighted lines. Never null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public static IReadOnlyList<StyledText> Highlight(string code, string language)
        {
            if (code == null)
                throw new ArgumentNullException(nameof(code));
            if (language == null)
                throw new ArgumentNullException(nameof(language));

            List<StyledText> lines = new List<StyledText>();
            string[] source = code.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            for (int i = 0; i < source.Length; i++)
                lines.Add(HighlightLine(source[i], language));

            return lines;
        }

        /// <summary>
        /// Highlights a single line of code.
        /// </summary>
        /// <param name="line">The line. Must not be null.</param>
        /// <param name="language">The language identifier. Must not be null.</param>
        /// <returns>The highlighted line. Never null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public static StyledText HighlightLine(string line, string language)
        {
            if (line == null)
                throw new ArgumentNullException(nameof(line));
            if (language == null)
                throw new ArgumentNullException(nameof(language));

            HashSet<string> keywords = KeywordsFor(language, out string commentMarker);
            List<StyledSpan> spans = new List<StyledSpan>();
            StringBuilder plain = new StringBuilder();

            int i = 0;
            while (i < line.Length)
            {
                char c = line[i];

                if (commentMarker.Length > 0 && Matches(line, i, commentMarker))
                {
                    Flush(spans, plain, CellStyle.Default);
                    spans.Add(new StyledSpan(line.Substring(i), _Comment));
                    break;
                }

                if (c == '"' || c == '\'')
                {
                    Flush(spans, plain, CellStyle.Default);
                    int start = i;
                    i++;
                    while (i < line.Length && line[i] != c)
                    {
                        if (line[i] == '\\' && i + 1 < line.Length)
                            i++;
                        i++;
                    }

                    if (i < line.Length)
                        i++;

                    spans.Add(new StyledSpan(line.Substring(start, i - start), _String));
                    continue;
                }

                if (char.IsDigit(c))
                {
                    Flush(spans, plain, CellStyle.Default);
                    int start = i;
                    while (i < line.Length && (char.IsDigit(line[i]) || line[i] == '.' || line[i] == 'x' || IsHex(line[i])))
                        i++;

                    spans.Add(new StyledSpan(line.Substring(start, i - start), _Number));
                    continue;
                }

                if (char.IsLetter(c) || c == '_')
                {
                    int start = i;
                    while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '_'))
                        i++;

                    string word = line.Substring(start, i - start);
                    if (keywords.Contains(word))
                    {
                        Flush(spans, plain, CellStyle.Default);
                        spans.Add(new StyledSpan(word, _Keyword));
                    }
                    else
                    {
                        plain.Append(word);
                    }

                    continue;
                }

                plain.Append(c);
                i++;
            }

            Flush(spans, plain, CellStyle.Default);
            return spans.Count == 0 ? StyledText.Empty : new StyledText(spans);
        }

        private static HashSet<string> KeywordsFor(string language, out string commentMarker)
        {
            switch (language.ToLowerInvariant())
            {
                case "csharp":
                case "c#":
                case "cs":
                    commentMarker = "//";
                    return _CSharp;
                case "javascript":
                case "js":
                case "typescript":
                case "ts":
                    commentMarker = "//";
                    return _Js;
                case "python":
                case "py":
                    commentMarker = "#";
                    return _Python;
                case "json":
                    commentMarker = string.Empty;
                    return _Json;
                default:
                    commentMarker = "#";
                    return _Json;
            }
        }

        private static void Flush(List<StyledSpan> spans, StringBuilder plain, CellStyle style)
        {
            if (plain.Length == 0)
                return;

            spans.Add(new StyledSpan(plain.ToString(), style));
            plain.Clear();
        }

        private static bool Matches(string text, int index, string token)
        {
            if (index + token.Length > text.Length)
                return false;

            for (int i = 0; i < token.Length; i++)
            {
                if (text[index + i] != token[i])
                    return false;
            }

            return true;
        }

        private static bool IsHex(char c)
        {
            return (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
        }

        private static HashSet<string> Keywords(string words)
        {
            return new HashSet<string>(words.Split(' '), StringComparer.Ordinal);
        }
    }
}

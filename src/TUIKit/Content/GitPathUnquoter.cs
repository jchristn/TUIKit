namespace TUIKit.Content
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Reads the file paths of unified diff headers: C-style quoted paths (which git emits for names with spaces,
    /// quotes, or non-ASCII bytes), a trailing tab and timestamp from <c>diff -u</c>, and the <c>a/</c> and
    /// <c>b/</c> prefixes. Stateless and thread-safe.
    /// </summary>
    internal static class GitPathUnquoter
    {
        internal static string HeaderPath(string rest)
        {
            string path = rest.Length > 0 && rest[0] == '"' ? Unquote(rest, 0, out _) : StripTimestamp(rest);
            return StripPrefix(path);
        }

        internal static string GitLinePath(string rest)
        {
            if (rest.Length > 0 && rest[0] == '"')
            {
                Unquote(rest, 0, out int end);
                string second = rest.Substring(Math.Min(rest.Length, end)).TrimStart();
                if (second.Length > 0)
                    return StripPrefix(second[0] == '"' ? Unquote(second, 0, out _) : second);
            }

            int b = rest.LastIndexOf(" b/", StringComparison.Ordinal);
            if (b >= 0)
                return rest.Substring(b + 3);

            int q = rest.LastIndexOf(" \"", StringComparison.Ordinal);
            if (q >= 0)
                return StripPrefix(Unquote(rest, q + 1, out _));

            return StripPrefix(rest);
        }

        internal static string Unquote(string text, int start, out int end)
        {
            List<byte> bytes = new List<byte>();
            int i = start + 1;
            while (i < text.Length && text[i] != '"')
            {
                char c = text[i];
                if (c == '\\' && i + 1 < text.Length)
                {
                    char e = text[i + 1];
                    if (e >= '0' && e <= '7')
                    {
                        int value = 0;
                        int digits = 0;
                        while (digits < 3 && i + 1 < text.Length && text[i + 1] >= '0' && text[i + 1] <= '7')
                        {
                            value = value * 8 + (text[i + 1] - '0');
                            i++;
                            digits++;
                        }

                        bytes.Add((byte)value);
                        i++;
                        continue;
                    }

                    bytes.AddRange(Encoding.UTF8.GetBytes(Escape(e).ToString()));
                    i += 2;
                    continue;
                }

                bytes.AddRange(Encoding.UTF8.GetBytes(c.ToString()));
                i++;
            }

            end = Math.Min(text.Length, i + 1);
            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        private static char Escape(char e)
        {
            switch (e)
            {
                case 'n':
                    return '\n';
                case 't':
                    return '\t';
                case 'r':
                    return '\r';
                case 'a':
                    return '\a';
                case 'b':
                    return '\b';
                case 'f':
                    return '\f';
                case 'v':
                    return '\v';
                default:
                    return e;
            }
        }

        private static string StripTimestamp(string path)
        {
            int tab = path.IndexOf('\t');
            return tab >= 0 ? path.Substring(0, tab) : path.TrimEnd();
        }

        private static string StripPrefix(string path)
        {
            if (path.StartsWith("a/", StringComparison.Ordinal) || path.StartsWith("b/", StringComparison.Ordinal))
                return path.Substring(2);
            return path;
        }
    }
}

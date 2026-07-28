namespace TUIKit.Content
{
    using System;
    using System.Text;

    /// <summary>
    /// Removes ANSI/VT escape sequences from text on ingest. TUIKit styles content through its own
    /// span API rather than interpreting embedded escapes, so incoming sequences from subprocesses or
    /// models are stripped to prevent them corrupting the cell grid. All members are thread-safe.
    /// </summary>
    public static class AnsiStripper
    {
        private const char Escape = '\u001b';
        private const char Bell = '\u0007';

        /// <summary>
        /// Returns a copy of the input with CSI and OSC escape sequences removed. Printable text,
        /// including newlines and tabs, is preserved.
        /// </summary>
        /// <param name="text">The text to strip. Must not be null.</param>
        /// <returns>The text with escape sequences removed. Never null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public static string Strip(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            if (text.IndexOf(Escape) < 0)
                return text;

            StringBuilder builder = new StringBuilder(text.Length);
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (c != Escape)
                {
                    builder.Append(c);
                    i++;
                    continue;
                }

                if (i + 1 >= text.Length)
                    break;

                char next = text[i + 1];
                if (next == '[')
                {
                    i += 2;
                    while (i < text.Length && !IsCsiFinal(text[i]))
                        i++;
                    if (i < text.Length)
                        i++;
                }
                else if (next == ']')
                {
                    i += 2;
                    while (i < text.Length)
                    {
                        if (text[i] == Bell)
                        {
                            i++;
                            break;
                        }

                        if (text[i] == Escape && i + 1 < text.Length && text[i + 1] == '\\')
                        {
                            i += 2;
                            break;
                        }

                        i++;
                    }
                }
                else
                {
                    // A two-character escape (for example ESC c); drop both characters.
                    i += 2;
                }
            }

            return builder.ToString();
        }

        private static bool IsCsiFinal(char c)
        {
            return c >= '@' && c <= '~';
        }
    }
}

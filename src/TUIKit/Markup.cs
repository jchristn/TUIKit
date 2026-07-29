namespace TUIKit
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;

    /// <summary>
    /// Parses a lightweight inline markup language into <see cref="StyledText"/>. Tags are written in
    /// square brackets and closed with <c>[/]</c>, and may nest: for example
    /// <c>"[bold red]error[/] [dim]on line 3[/]"</c>. A tag is a space-separated list of attributes
    /// (<c>bold</c>, <c>dim</c>, <c>italic</c>, <c>underline</c>, <c>strikethrough</c>/<c>strike</c>,
    /// <c>reverse</c>), an optional foreground color, and an optional <c>on &lt;color&gt;</c> background.
    /// Colors are named (<c>red</c>, <c>green</c>, …), a <c>#RRGGBB</c> hex value, or a palette index.
    /// Write a literal bracket as <c>[[</c> or <c>]]</c>. All members are thread-safe.
    /// </summary>
    public static class Markup
    {
        /// <summary>
        /// Parses markup into styled text using the default base style.
        /// </summary>
        /// <param name="text">The markup source. Must not be null.</param>
        /// <returns>The styled text. Never null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public static StyledText Parse(string text)
        {
            return Parse(text, CellStyle.Default);
        }

        /// <summary>
        /// Parses markup into styled text, composing tags over a base style.
        /// </summary>
        /// <param name="text">The markup source. Must not be null.</param>
        /// <param name="baseStyle">The base style that unmarked text uses.</param>
        /// <returns>The styled text. Never null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public static StyledText Parse(string text, CellStyle baseStyle)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            List<StyledSpan> spans = new List<StyledSpan>();
            StringBuilder run = new StringBuilder();
            Stack<CellStyle> stack = new Stack<CellStyle>();
            stack.Push(baseStyle);

            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];

                if (c == '[')
                {
                    if (i + 1 < text.Length && text[i + 1] == '[')
                    {
                        run.Append('[');
                        i += 2;
                        continue;
                    }

                    int close = text.IndexOf(']', i + 1);
                    if (close < 0)
                    {
                        run.Append(c);
                        i++;
                        continue;
                    }

                    FlushRun(spans, run, stack.Peek());
                    string tag = text.Substring(i + 1, close - i - 1);
                    if (tag == "/")
                    {
                        if (stack.Count > 1)
                            stack.Pop();
                    }
                    else
                    {
                        stack.Push(ApplyTag(stack.Peek(), tag));
                    }

                    i = close + 1;
                    continue;
                }

                if (c == ']' && i + 1 < text.Length && text[i + 1] == ']')
                {
                    run.Append(']');
                    i += 2;
                    continue;
                }

                run.Append(c);
                i++;
            }

            FlushRun(spans, run, stack.Peek());
            return spans.Count == 0 ? StyledText.Empty : new StyledText(spans);
        }

        private static void FlushRun(List<StyledSpan> spans, StringBuilder run, CellStyle style)
        {
            if (run.Length == 0)
                return;

            spans.Add(new StyledSpan(run.ToString(), style));
            run.Clear();
        }

        private static CellStyle ApplyTag(CellStyle baseStyle, string tag)
        {
            CellStyle style = baseStyle;
            string[] tokens = tag.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i].ToLowerInvariant();
                switch (token)
                {
                    case "bold":
                        style = style.WithAttribute(CellAttributes.Bold, true);
                        break;
                    case "dim":
                        style = style.WithAttribute(CellAttributes.Dim, true);
                        break;
                    case "italic":
                        style = style.WithAttribute(CellAttributes.Italic, true);
                        break;
                    case "underline":
                        style = style.WithAttribute(CellAttributes.Underline, true);
                        break;
                    case "strike":
                    case "strikethrough":
                        style = style.WithAttribute(CellAttributes.Strikethrough, true);
                        break;
                    case "reverse":
                        style = style.WithAttribute(CellAttributes.Reverse, true);
                        break;
                    case "on":
                        if (i + 1 < tokens.Length && TryColor(tokens[i + 1], out Color background))
                        {
                            style = style.WithBackground(background);
                            i++;
                        }

                        break;
                    default:
                        if (TryColor(token, out Color foreground))
                            style = style.WithForeground(foreground);
                        break;
                }
            }

            return style;
        }

        private static bool TryColor(string token, out Color color)
        {
            color = Color.Default;
            if (string.IsNullOrEmpty(token))
                return false;

            if (token[0] == '#' && token.Length == 7)
            {
                if (int.TryParse(token.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
                {
                    color = Color.FromRgb(rgb);
                    return true;
                }

                return false;
            }

            switch (token)
            {
                case "black":
                    color = Color.FromPalette(0);
                    return true;
                case "red":
                    color = Color.FromPalette(1);
                    return true;
                case "green":
                    color = Color.FromPalette(2);
                    return true;
                case "yellow":
                    color = Color.FromPalette(3);
                    return true;
                case "blue":
                    color = Color.FromPalette(4);
                    return true;
                case "magenta":
                    color = Color.FromPalette(5);
                    return true;
                case "cyan":
                    color = Color.FromPalette(6);
                    return true;
                case "white":
                    color = Color.FromPalette(7);
                    return true;
                case "gray":
                case "grey":
                    color = Color.FromPalette(8);
                    return true;
                default:
                    if (byte.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte index))
                    {
                        color = Color.FromPalette(index);
                        return true;
                    }

                    return false;
            }
        }
    }
}

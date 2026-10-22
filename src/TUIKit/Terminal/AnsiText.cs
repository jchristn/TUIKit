namespace TUIKit.Terminal
{
    using System;
    using System.Text;
    using TUIKit;

    /// <summary>
    /// Renders <see cref="StyledText"/> to a flowing string of SGR escape sequences and text, with a
    /// trailing reset, quantized to a <see cref="TerminalColorDepth"/>. Unlike the full-screen
    /// <see cref="TUIKit.Rendering.TerminalRenderer"/>, this emits <b>no cursor movement</b> — the
    /// result is meant to be written at the current cursor position (for example by
    /// <see cref="TUIKit.StyledConsole"/>). When the depth is <see cref="TerminalColorDepth.None"/>,
    /// the plain text is returned with no escape sequences. All members are thread-safe.
    /// </summary>
    public static class AnsiText
    {
        /// <summary>
        /// Renders styled text to an ANSI string. Each span's style is emitted with
        /// <see cref="Ansi.Sgr(CellStyle, TerminalColorDepth)"/> before its text, followed by a single
        /// trailing <see cref="Ansi.ResetAttributes"/>.
        /// </summary>
        /// <param name="text">The styled text. Must not be null.</param>
        /// <param name="depth">The color depth to quantize to.</param>
        /// <returns>The ANSI-styled string; plain text when <paramref name="depth"/> is <see cref="TerminalColorDepth.None"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public static string Render(StyledText text, TerminalColorDepth depth)
        {
            return Render(text, depth, false);
        }

        /// <summary>Renders text with independently selected color and OSC 8 output.</summary>
        /// <param name="text">The text. Must not be null.</param>
        /// <param name="depth">The color depth; None suppresses SGR only.</param>
        /// <param name="hyperlinks">Whether to emit hyperlinks. Defaults are selected by the caller.</param>
        /// <returns>The rendered text, with links closed before newlines and at the end.</returns>
        /// <exception cref="ArgumentNullException">The text is null.</exception>
        public static string Render(StyledText text, TerminalColorDepth depth, bool hyperlinks)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            if (depth == TerminalColorDepth.None && !hyperlinks)
                return text.ToPlainString();

            System.Collections.Generic.IReadOnlyList<StyledSpan> spans = text.Spans;
            if (spans.Count == 0)
                return string.Empty;

            StringBuilder builder = new StringBuilder();
            bool wroteAny = false;
            string? currentLink = null;
            for (int i = 0; i < spans.Count; i++)
            {
                StyledSpan span = spans[i];
                if (span.Text.Length == 0)
                    continue;

                if (depth != TerminalColorDepth.None)
                    builder.Append(Ansi.Sgr(span.Style, depth));
                if (!hyperlinks || span.Style.Hyperlink == null)
                {
                    HyperlinkOutput.Transition(builder, ref currentLink, null);
                    builder.Append(span.Text);
                }
                else
                {
                    for (int j = 0; j < span.Text.Length; j++)
                    {
                        char c = span.Text[j];
                        HyperlinkOutput.Transition(builder, ref currentLink,
                            c != '\r' && c != '\n' ? span.Style.Hyperlink : null);
                        builder.Append(c);
                    }
                }
                wroteAny = true;
            }

            HyperlinkOutput.Transition(builder, ref currentLink, null);
            if (wroteAny && depth != TerminalColorDepth.None)
                builder.Append(Ansi.ResetAttributes);

            return builder.ToString();
        }

        /// <summary>
        /// Parses markup and renders it. Equivalent to <c>Render(Markup.Parse(markup), depth)</c>.
        /// </summary>
        /// <param name="markup">The markup source. Must not be null.</param>
        /// <param name="depth">The color depth to quantize to.</param>
        /// <returns>The ANSI-styled string.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="markup"/> is null.</exception>
        public static string Render(string markup, TerminalColorDepth depth)
        {
            if (markup == null)
                throw new ArgumentNullException(nameof(markup));

            return Render(Markup.Parse(markup), depth);
        }
    }
}

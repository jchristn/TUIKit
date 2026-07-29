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
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            if (depth == TerminalColorDepth.None)
                return text.ToPlainString();

            System.Collections.Generic.IReadOnlyList<StyledSpan> spans = text.Spans;
            if (spans.Count == 0)
                return string.Empty;

            StringBuilder builder = new StringBuilder();
            bool wroteAny = false;
            for (int i = 0; i < spans.Count; i++)
            {
                StyledSpan span = spans[i];
                if (span.Text.Length == 0)
                    continue;

                builder.Append(Ansi.Sgr(span.Style, depth));
                builder.Append(span.Text);
                wroteAny = true;
            }

            if (wroteAny)
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

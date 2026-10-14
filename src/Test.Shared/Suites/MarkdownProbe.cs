namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Content;

    /// <summary>
    /// Helpers for the width-aware Markdown suites: render to plain rows, find the style of a piece of text, and
    /// assert the width invariant. Stateless and thread-safe.
    /// </summary>
    public static class MarkdownProbe
    {
        /// <summary>
        /// Renders Markdown at a width and returns each row as plain text.
        /// </summary>
        /// <param name="markdown">The Markdown source. Must not be null.</param>
        /// <param name="width">The available width.</param>
        /// <param name="options">The options, or null for defaults.</param>
        /// <returns>The plain rows. Never null.</returns>
        public static List<string> Plain(string markdown, int width, MarkdownRenderOptions? options = null)
        {
            IReadOnlyList<StyledText> rows = MarkdownRenderer.Render(markdown, width, options ?? new MarkdownRenderOptions());
            List<string> plain = new List<string>(rows.Count);
            for (int i = 0; i < rows.Count; i++)
                plain.Add(rows[i].ToPlainString());

            return plain;
        }

        /// <summary>
        /// Joins rows with newlines, for containment checks and failure messages.
        /// </summary>
        /// <param name="rows">The rows. Must not be null.</param>
        /// <returns>The joined text. Never null.</returns>
        public static string Join(IReadOnlyList<string> rows)
        {
            return string.Join("\n", rows);
        }

        /// <summary>
        /// Returns the index of the first row containing the text, or -1.
        /// </summary>
        /// <param name="rows">The rows. Must not be null.</param>
        /// <param name="text">The text to find. Must not be null.</param>
        /// <returns>The row index, or -1.</returns>
        public static int RowOf(IReadOnlyList<string> rows, string text)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Contains(text))
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Returns the style of the first span whose text contains the given text.
        /// </summary>
        /// <param name="rows">The styled rows. Must not be null.</param>
        /// <param name="text">The text to find. Must not be null.</param>
        /// <returns>The span's style.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no span contains the text.</exception>
        public static CellStyle StyleOf(IReadOnlyList<StyledText> rows, string text)
        {
            for (int r = 0; r < rows.Count; r++)
            {
                IReadOnlyList<StyledSpan> spans = rows[r].Spans;
                for (int s = 0; s < spans.Count; s++)
                {
                    if (spans[s].Text.Contains(text))
                        return spans[s].Style;
                }
            }

            throw new InvalidOperationException("No span contains '" + text + "'.");
        }

        /// <summary>
        /// Asserts that every row is at most the expected layout width.
        /// </summary>
        /// <param name="rows">The styled rows. Must not be null.</param>
        /// <param name="width">The layout width.</param>
        /// <param name="context">What was rendered, for the failure message.</param>
        /// <exception cref="InvalidOperationException">Thrown when a row is wider than the width.</exception>
        public static void AssertFits(IReadOnlyList<StyledText> rows, int width, string context)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Width > width)
                    throw new InvalidOperationException(context + ": row " + i + " is " + rows[i].Width + " wide at width " + width + ": '" + rows[i].ToPlainString() + "'.");
            }
        }
    }
}

namespace TUIKit.Unicode
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Cell-width aware string fitting: measuring, truncating, ellipsizing, padding, and slicing text by
    /// terminal columns rather than by <see cref="string.Length"/>. Every member honors grapheme clusters
    /// and wide (CJK, emoji) glyphs, so a two-column glyph is never split and padded text always fills
    /// exactly the requested number of columns. All members are thread-safe and allocate only their result.
    /// </summary>
    public static class TextFit
    {
        /// <summary>
        /// The default ellipsis appended by <see cref="Ellipsize(string, int)"/> (U+2026, one column).
        /// </summary>
        public const string DefaultEllipsis = "\u2026";

        /// <summary>
        /// Measures the terminal column width of text. Null is treated as empty.
        /// </summary>
        /// <param name="text">The text, or null.</param>
        /// <returns>The number of columns the text occupies; zero for null or empty text.</returns>
        public static int Width(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            return Graphemes.MeasureWidth(text!);
        }

        /// <summary>
        /// Truncates text to at most <paramref name="maxWidth"/> columns without an ellipsis. A wide glyph
        /// that would straddle the limit is dropped rather than split. Null is treated as empty.
        /// </summary>
        /// <param name="text">The text, or null.</param>
        /// <param name="maxWidth">The maximum width in columns. Values below 1 yield an empty string.</param>
        /// <returns>The fitted text. Never null.</returns>
        public static string Truncate(string? text, int maxWidth)
        {
            if (string.IsNullOrEmpty(text) || maxWidth < 1)
                return string.Empty;

            IReadOnlyList<Grapheme> clusters = Graphemes.Split(text!);
            StringBuilder builder = new StringBuilder(text!.Length);
            int used = 0;
            for (int i = 0; i < clusters.Count; i++)
            {
                if (used + clusters[i].Width > maxWidth)
                    break;

                builder.Append(clusters[i].Text);
                used += clusters[i].Width;
            }

            return builder.ToString();
        }

        /// <summary>
        /// Truncates text to at most <paramref name="maxWidth"/> columns, ending with
        /// <see cref="DefaultEllipsis"/> when it had to be clipped. Text that already fits is returned
        /// unchanged. Null is treated as empty.
        /// </summary>
        /// <param name="text">The text, or null.</param>
        /// <param name="maxWidth">The maximum width in columns. Values below 1 yield an empty string.</param>
        /// <returns>The fitted text. Never null.</returns>
        public static string Ellipsize(string? text, int maxWidth)
        {
            return Ellipsize(text, maxWidth, DefaultEllipsis);
        }

        /// <summary>
        /// Truncates text to at most <paramref name="maxWidth"/> columns, ending with
        /// <paramref name="ellipsis"/> when it had to be clipped. When the ellipsis alone is wider than
        /// the limit the text is truncated without one. Null text is treated as empty.
        /// </summary>
        /// <param name="text">The text, or null.</param>
        /// <param name="maxWidth">The maximum width in columns. Values below 1 yield an empty string.</param>
        /// <param name="ellipsis">The marker appended when the text is clipped. Null is treated as empty.</param>
        /// <returns>The fitted text. Never null.</returns>
        public static string Ellipsize(string? text, int maxWidth, string? ellipsis)
        {
            if (string.IsNullOrEmpty(text) || maxWidth < 1)
                return string.Empty;

            if (Graphemes.MeasureWidth(text!) <= maxWidth)
                return text!;

            string marker = ellipsis ?? string.Empty;
            int markerWidth = Width(marker);
            if (markerWidth >= maxWidth)
                return Truncate(text, maxWidth);

            return Truncate(text, maxWidth - markerWidth) + marker;
        }

        /// <summary>
        /// Pads text on the right with spaces to exactly <paramref name="width"/> columns, truncating it
        /// first when it is wider. Null is treated as empty.
        /// </summary>
        /// <param name="text">The text, or null.</param>
        /// <param name="width">The target width in columns. Values below 1 yield an empty string.</param>
        /// <returns>Text occupying exactly <paramref name="width"/> columns. Never null.</returns>
        public static string PadRight(string? text, int width)
        {
            if (width < 1)
                return string.Empty;

            string fitted = Truncate(text, width);
            int used = Width(fitted);
            return used < width ? fitted + new string(' ', width - used) : fitted;
        }

        /// <summary>
        /// Pads text on the left with spaces to exactly <paramref name="width"/> columns (right
        /// alignment), truncating it first when it is wider. Null is treated as empty.
        /// </summary>
        /// <param name="text">The text, or null.</param>
        /// <param name="width">The target width in columns. Values below 1 yield an empty string.</param>
        /// <returns>Text occupying exactly <paramref name="width"/> columns. Never null.</returns>
        public static string PadLeft(string? text, int width)
        {
            if (width < 1)
                return string.Empty;

            string fitted = Truncate(text, width);
            int used = Width(fitted);
            return used < width ? new string(' ', width - used) + fitted : fitted;
        }

        /// <summary>
        /// Centers text within exactly <paramref name="width"/> columns, truncating it first when it is
        /// wider. An odd leftover column goes to the right. Null is treated as empty.
        /// </summary>
        /// <param name="text">The text, or null.</param>
        /// <param name="width">The target width in columns. Values below 1 yield an empty string.</param>
        /// <returns>Text occupying exactly <paramref name="width"/> columns. Never null.</returns>
        public static string Center(string? text, int width)
        {
            if (width < 1)
                return string.Empty;

            string fitted = Truncate(text, width);
            int spare = width - Width(fitted);
            int left = spare / 2;
            return new string(' ', left) + fitted + new string(' ', spare - left);
        }

        /// <summary>
        /// Returns the columns of text in the half-open range [<paramref name="startColumn"/>,
        /// <paramref name="startColumn"/> + <paramref name="width"/>), for horizontal scrolling. A wide
        /// glyph cut by either edge is replaced by a space so the result never misaligns. Null is treated
        /// as empty.
        /// </summary>
        /// <param name="text">The text, or null.</param>
        /// <param name="startColumn">The first column to keep. Negative values are treated as zero.</param>
        /// <param name="width">The number of columns to keep. Values below 1 yield an empty string.</param>
        /// <returns>The sliced text, at most <paramref name="width"/> columns wide. Never null.</returns>
        public static string Slice(string? text, int startColumn, int width)
        {
            if (string.IsNullOrEmpty(text) || width < 1)
                return string.Empty;

            int start = Math.Max(0, startColumn);
            int end = start + width;
            IReadOnlyList<Grapheme> clusters = Graphemes.Split(text!);
            StringBuilder builder = new StringBuilder();
            int column = 0;
            for (int i = 0; i < clusters.Count && column < end; i++)
            {
                Grapheme cluster = clusters[i];
                int next = column + cluster.Width;
                if (cluster.Width == 0)
                {
                    if (column >= start && column < end && builder.Length > 0)
                        builder.Append(cluster.Text);
                }
                else if (column >= start && next <= end)
                {
                    builder.Append(cluster.Text);
                }
                else if (next > start && column < end)
                {
                    // A wide glyph straddles an edge: keep the visible columns as spaces.
                    int visible = Math.Min(next, end) - Math.Max(column, start);
                    builder.Append(' ', visible);
                }

                column = next;
            }

            return builder.ToString();
        }

        /// <summary>
        /// Returns the column at which the UTF-16 index <paramref name="charIndex"/> of text begins, for
        /// placing a caret or cursor. Indexes past the end measure the whole text. Null is treated as empty.
        /// </summary>
        /// <param name="text">The text, or null.</param>
        /// <param name="charIndex">The UTF-16 index. Values below zero yield zero.</param>
        /// <returns>The column offset of the index.</returns>
        public static int ColumnOf(string? text, int charIndex)
        {
            if (string.IsNullOrEmpty(text) || charIndex <= 0)
                return 0;

            int length = Math.Min(charIndex, text!.Length);
            return Graphemes.MeasureWidth(text.Substring(0, length));
        }

        /// <summary>
        /// Returns the UTF-16 index of the grapheme cluster that covers <paramref name="column"/>, the
        /// inverse of <see cref="ColumnOf"/>, for mapping a click back to a caret position. Columns past the
        /// end map to the text length. Null is treated as empty.
        /// </summary>
        /// <param name="text">The text, or null.</param>
        /// <param name="column">The column. Values below zero yield zero.</param>
        /// <returns>The UTF-16 index at a grapheme boundary.</returns>
        public static int IndexAtColumn(string? text, int column)
        {
            if (string.IsNullOrEmpty(text) || column <= 0)
                return 0;

            IReadOnlyList<Grapheme> clusters = Graphemes.Split(text!);
            int used = 0;
            int index = 0;
            for (int i = 0; i < clusters.Count; i++)
            {
                if (used + clusters[i].Width > column)
                    return index;

                used += clusters[i].Width;
                index += clusters[i].Text.Length;
            }

            return text!.Length;
        }

        /// <summary>
        /// Returns the UTF-16 index of the grapheme boundary after <paramref name="charIndex"/>, so caret
        /// movement steps over a whole cluster (a surrogate pair, an emoji ZWJ sequence, a base plus
        /// combining marks). Null is treated as empty.
        /// </summary>
        /// <param name="text">The text, or null.</param>
        /// <param name="charIndex">The current UTF-16 index.</param>
        /// <returns>The next boundary, or the text length when already at the end.</returns>
        public static int NextBoundary(string? text, int charIndex)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            IReadOnlyList<Grapheme> clusters = Graphemes.Split(text!);
            int index = 0;
            for (int i = 0; i < clusters.Count; i++)
            {
                int next = index + clusters[i].Text.Length;
                if (next > charIndex)
                    return next;

                index = next;
            }

            return text!.Length;
        }

        /// <summary>
        /// Returns the UTF-16 index of the grapheme boundary before <paramref name="charIndex"/>, the
        /// inverse of <see cref="NextBoundary"/>. Null is treated as empty.
        /// </summary>
        /// <param name="text">The text, or null.</param>
        /// <param name="charIndex">The current UTF-16 index.</param>
        /// <returns>The previous boundary, or zero when already at the start.</returns>
        public static int PreviousBoundary(string? text, int charIndex)
        {
            if (string.IsNullOrEmpty(text) || charIndex <= 0)
                return 0;

            IReadOnlyList<Grapheme> clusters = Graphemes.Split(text!);
            int index = 0;
            int previous = 0;
            for (int i = 0; i < clusters.Count; i++)
            {
                if (index >= charIndex)
                    return previous;

                previous = index;
                index += clusters[i].Text.Length;
            }

            return index >= charIndex ? previous : index;
        }
    }
}

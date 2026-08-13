namespace TUIKit.Content
{
    using System;
    using System.Collections.Generic;
    using TUIKit.Unicode;

    /// <summary>
    /// Wraps a separator-delimited hint string (for example a footer of
    /// "Enter: ok · Esc: cancel · Tab: next") to a column width without ever splitting a single
    /// segment. Segments are packed greedily onto each line and the separator is reinserted between
    /// segments that share a line, so a long hint set wraps to more lines instead of being truncated.
    /// All members are thread-safe.
    /// </summary>
    public static class HintText
    {
        /// <summary>
        /// Wraps a hint string into lines no wider than <paramref name="width"/> columns.
        /// </summary>
        /// <param name="hint">The hint text. Must not be null.</param>
        /// <param name="width">The maximum line width in columns. Must be greater than zero.</param>
        /// <param name="separator">The segment separator. Defaults to " · ".</param>
        /// <returns>
        /// The wrapped lines. Never null; a hint with no segments yields an empty list. A single segment
        /// wider than <paramref name="width"/> occupies its own line unbroken.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="hint"/> or <paramref name="separator"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="width"/> is not positive.</exception>
        public static IReadOnlyList<string> Wrap(string hint, int width, string separator = " · ")
        {
            if (hint == null)
                throw new ArgumentNullException(nameof(hint));
            if (separator == null)
                throw new ArgumentNullException(nameof(separator));
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be greater than zero.");

            List<string> lines = new List<string>();
            string[] segments = hint.Split(new[] { separator }, StringSplitOptions.None);
            int separatorWidth = Graphemes.MeasureWidth(separator);

            string current = string.Empty;
            int currentWidth = 0;
            for (int i = 0; i < segments.Length; i++)
            {
                string segment = segments[i].Trim();
                if (segment.Length == 0)
                    continue;

                int segmentWidth = Graphemes.MeasureWidth(segment);
                if (current.Length == 0)
                {
                    current = segment;
                    currentWidth = segmentWidth;
                    continue;
                }

                if (currentWidth + separatorWidth + segmentWidth <= width)
                {
                    current = current + separator + segment;
                    currentWidth += separatorWidth + segmentWidth;
                }
                else
                {
                    lines.Add(current);
                    current = segment;
                    currentWidth = segmentWidth;
                }
            }

            if (current.Length > 0)
                lines.Add(current);

            return lines;
        }
    }
}

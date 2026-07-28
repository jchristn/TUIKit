namespace TUIKit.Testing
{
    using System;
    using System.Text;
    using TUIKit;
    using TUIKit.Widgets;

    /// <summary>
    /// Renders a cell buffer to plain text for snapshot assertions. Consumers can render their own UI
    /// to a <see cref="CellBuffer"/> and compare the text against a golden, the same way TUIKit tests
    /// itself. All members are thread-safe.
    /// </summary>
    public static class Snapshot
    {
        /// <summary>
        /// Converts a cell buffer to a newline-separated string, one line per row, with trailing
        /// spaces trimmed.
        /// </summary>
        /// <param name="buffer">The buffer to snapshot. Must not be null.</param>
        /// <returns>The text representation. Never null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="buffer"/> is null.</exception>
        public static string ToText(CellBuffer buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            StringBuilder builder = new StringBuilder();
            for (int y = 0; y < buffer.Height; y++)
            {
                StringBuilder row = new StringBuilder();
                for (int x = 0; x < buffer.Width; x++)
                {
                    Cell cell = buffer.Get(x, y);
                    if (!cell.IsContinuation)
                        row.Append(string.IsNullOrEmpty(cell.Grapheme) ? " " : cell.Grapheme);
                }

                builder.Append(TrimEnd(row.ToString()));
                if (y < buffer.Height - 1)
                    builder.Append('\n');
            }

            return builder.ToString();
        }

        /// <summary>
        /// Renders a widget into a buffer of the supplied size and returns its text snapshot.
        /// </summary>
        /// <param name="widget">The widget to render. Must not be null.</param>
        /// <param name="width">The width in cells. Must be greater than zero.</param>
        /// <param name="height">The height in cells. Must be greater than zero.</param>
        /// <returns>The text snapshot. Never null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="widget"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a dimension is not positive.</exception>
        public static string RenderWidget(IWidget widget, int width, int height)
        {
            if (widget == null)
                throw new ArgumentNullException(nameof(widget));
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be greater than zero.");
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be greater than zero.");

            CellBuffer buffer = new CellBuffer(width, height);
            widget.Render(new BufferSurface(buffer));
            return ToText(buffer);
        }

        private static string TrimEnd(string value)
        {
            int end = value.Length;
            while (end > 0 && value[end - 1] == ' ')
                end--;

            return value.Substring(0, end);
        }
    }
}

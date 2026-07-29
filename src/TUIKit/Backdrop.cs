namespace TUIKit
{
    using System;

    /// <summary>
    /// Dims a region of a rendered buffer in place, typically the whole screen behind a modal so the
    /// dialog stands out. It rewrites each cell's style with the <see cref="CellAttributes.Dim"/>
    /// attribute, preserving the glyphs underneath. Apply it to the composed buffer just before
    /// drawing the modal on top.
    /// </summary>
    public static class Backdrop
    {
        /// <summary>
        /// Dims every cell of a buffer.
        /// </summary>
        /// <param name="buffer">The buffer to dim. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="buffer"/> is null.</exception>
        public static void Dim(CellBuffer buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            Dim(buffer, new Rect(0, 0, buffer.Width, buffer.Height));
        }

        /// <summary>
        /// Dims every cell within a region of a buffer. The region is clipped to the buffer.
        /// </summary>
        /// <param name="buffer">The buffer to dim. Must not be null.</param>
        /// <param name="region">The region to dim, in buffer coordinates.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="buffer"/> is null.</exception>
        public static void Dim(CellBuffer buffer, Rect region)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            int x0 = Math.Max(0, region.X);
            int y0 = Math.Max(0, region.Y);
            int x1 = Math.Min(buffer.Width, region.X + region.Width);
            int y1 = Math.Min(buffer.Height, region.Y + region.Height);

            for (int y = y0; y < y1; y++)
            {
                for (int x = x0; x < x1; x++)
                {
                    Cell cell = buffer.Get(x, y);
                    if (cell.Style.HasAttribute(CellAttributes.Dim))
                        continue;

                    buffer.Set(x, y, Rebuild(cell));
                }
            }
        }

        private static Cell Rebuild(Cell cell)
        {
            CellStyle dimmed = cell.Style.WithAttribute(CellAttributes.Dim, true);
            if (cell.Width <= 0)
                return Cell.Continuation(dimmed);
            if (string.IsNullOrEmpty(cell.Grapheme) || cell.Grapheme == " ")
                return Cell.Blank(dimmed);

            return Cell.Glyph(cell.Grapheme, dimmed, cell.Width);
        }
    }
}

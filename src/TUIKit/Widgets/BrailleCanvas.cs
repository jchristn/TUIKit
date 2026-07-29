namespace TUIKit.Widgets
{
    using System;
    using System.Text;
    using TUIKit;

    /// <summary>
    /// A monochrome pixel canvas that packs a 2×4 dot grid into every terminal cell using Unicode
    /// Braille glyphs (U+2800–U+28FF). A canvas that is <c>w</c> cells wide and <c>h</c> cells tall
    /// therefore exposes a <c>2w × 4h</c> pixel surface, giving charts and sparklines far more
    /// resolution than character cells alone. Pixels are set, cleared, and connected with lines; the
    /// whole canvas renders in a single color. Braille glyphs are covered by the framework's Unicode
    /// width tables, so the canvas aligns identically across platforms, SSH, and tmux.
    /// </summary>
    public sealed class BrailleCanvas : IWidget
    {
        private static readonly byte[,] _DotBits =
        {
            { 0x01, 0x02, 0x04, 0x40 },
            { 0x08, 0x10, 0x20, 0x80 }
        };

        private readonly int _CellWidth;
        private readonly int _CellHeight;
        private readonly byte[] _Cells;

        /// <summary>
        /// Gets or sets the color used to draw set pixels. Defaults to cyan.
        /// </summary>
        public Color Color { get; set; } = Color.FromPalette(6);

        /// <summary>
        /// Gets the pixel width of the canvas (twice the cell width).
        /// </summary>
        public int PixelWidth
        {
            get { return _CellWidth * 2; }
        }

        /// <summary>
        /// Gets the pixel height of the canvas (four times the cell height).
        /// </summary>
        public int PixelHeight
        {
            get { return _CellHeight * 4; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BrailleCanvas"/> class.
        /// </summary>
        /// <param name="cellWidth">The canvas width in terminal cells. Must be positive.</param>
        /// <param name="cellHeight">The canvas height in terminal cells. Must be positive.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a dimension is not positive.</exception>
        public BrailleCanvas(int cellWidth, int cellHeight)
        {
            if (cellWidth <= 0)
                throw new ArgumentOutOfRangeException(nameof(cellWidth));
            if (cellHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(cellHeight));

            _CellWidth = cellWidth;
            _CellHeight = cellHeight;
            _Cells = new byte[cellWidth * cellHeight];
        }

        /// <summary>
        /// Clears every pixel.
        /// </summary>
        public void Clear()
        {
            Array.Clear(_Cells, 0, _Cells.Length);
        }

        /// <summary>
        /// Sets the pixel at the given coordinates. Coordinates outside the surface are ignored.
        /// </summary>
        /// <param name="x">The pixel x coordinate.</param>
        /// <param name="y">The pixel y coordinate.</param>
        public void Set(int x, int y)
        {
            if (!InRange(x, y))
                return;

            _Cells[Index(x, y)] |= _DotBits[x & 1, y & 3];
        }

        /// <summary>
        /// Clears the pixel at the given coordinates. Coordinates outside the surface are ignored.
        /// </summary>
        /// <param name="x">The pixel x coordinate.</param>
        /// <param name="y">The pixel y coordinate.</param>
        public void Unset(int x, int y)
        {
            if (!InRange(x, y))
                return;

            _Cells[Index(x, y)] &= (byte)~_DotBits[x & 1, y & 3];
        }

        /// <summary>
        /// Gets whether the pixel at the given coordinates is set.
        /// </summary>
        /// <param name="x">The pixel x coordinate.</param>
        /// <param name="y">The pixel y coordinate.</param>
        /// <returns><c>true</c> when the pixel is set; otherwise <c>false</c>.</returns>
        public bool Get(int x, int y)
        {
            if (!InRange(x, y))
                return false;

            return (_Cells[Index(x, y)] & _DotBits[x & 1, y & 3]) != 0;
        }

        /// <summary>
        /// Draws a line between two pixels with Bresenham's algorithm.
        /// </summary>
        /// <param name="x0">The start x coordinate.</param>
        /// <param name="y0">The start y coordinate.</param>
        /// <param name="x1">The end x coordinate.</param>
        /// <param name="y1">The end y coordinate.</param>
        public void Line(int x0, int y0, int x1, int y1)
        {
            int dx = Math.Abs(x1 - x0);
            int dy = -Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;

            while (true)
            {
                Set(x0, y0);
                if (x0 == x1 && y0 == y1)
                    break;

                int e2 = 2 * err;
                if (e2 >= dy)
                {
                    err += dy;
                    x0 += sx;
                }

                if (e2 <= dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            return new Size(Math.Min(available.Width, _CellWidth), Math.Min(available.Height, _CellHeight));
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            CellStyle style = CellStyle.Default.WithForeground(Color);
            int width = Math.Min(surface.Size.Width, _CellWidth);
            int height = Math.Min(surface.Size.Height, _CellHeight);

            for (int cy = 0; cy < height; cy++)
            {
                for (int cx = 0; cx < width; cx++)
                {
                    byte bits = _Cells[cy * _CellWidth + cx];
                    if (bits == 0)
                        continue;

                    char glyph = (char)(0x2800 + bits);
                    surface.DrawText(cx, cy, glyph.ToString(), style);
                }
            }
        }

        private bool InRange(int x, int y)
        {
            return x >= 0 && y >= 0 && x < PixelWidth && y < PixelHeight;
        }

        private int Index(int x, int y)
        {
            return (y / 4) * _CellWidth + (x / 2);
        }
    }
}

namespace TUIKit.Widgets
{
    using System;
    using TUIKit;

    /// <summary>
    /// Renders a truecolor pixel image in the terminal at double vertical resolution using the upper
    /// half-block glyph (▀): each cell packs two stacked pixels, the top as the glyph's foreground and
    /// the bottom as its background. A <c>w × h</c> pixel image therefore occupies <c>w</c> cells wide
    /// and <c>⌈h/2⌉</c> cells tall. Supply pixels as a 2D color array or a sampler function; decoding
    /// image files is left to the caller so the framework stays dependency-free.
    /// </summary>
    public sealed class HalfBlockImage : IWidget
    {
        private readonly Color[,] _Pixels;

        /// <summary>Gets the image width in pixels.</summary>
        public int PixelWidth { get; }

        /// <summary>Gets the image height in pixels.</summary>
        public int PixelHeight { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="HalfBlockImage"/> class from a color grid
        /// indexed as <c>[y, x]</c>.
        /// </summary>
        /// <param name="pixels">The pixel grid (row-major). Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="pixels"/> is null.</exception>
        public HalfBlockImage(Color[,] pixels)
        {
            if (pixels == null)
                throw new ArgumentNullException(nameof(pixels));

            _Pixels = pixels;
            PixelHeight = pixels.GetLength(0);
            PixelWidth = pixels.GetLength(1);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HalfBlockImage"/> class from a sampler.
        /// </summary>
        /// <param name="width">The image width in pixels. Must be positive.</param>
        /// <param name="height">The image height in pixels. Must be positive.</param>
        /// <param name="sampler">A function returning the color at (x, y). Must not be null.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a dimension is not positive.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="sampler"/> is null.</exception>
        public HalfBlockImage(int width, int height, Func<int, int, Color> sampler)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height));
            if (sampler == null)
                throw new ArgumentNullException(nameof(sampler));

            _Pixels = new Color[height, width];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                    _Pixels[y, x] = sampler(x, y);
            }

            PixelWidth = width;
            PixelHeight = height;
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            return new Size(Math.Min(available.Width, PixelWidth), Math.Min(available.Height, (PixelHeight + 1) / 2));
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            int cellRows = (PixelHeight + 1) / 2;
            int width = Math.Min(surface.Size.Width, PixelWidth);
            int height = Math.Min(surface.Size.Height, cellRows);

            for (int cy = 0; cy < height; cy++)
            {
                int topY = cy * 2;
                int bottomY = topY + 1;
                for (int x = 0; x < width; x++)
                {
                    Color top = _Pixels[topY, x];
                    Color bottom = bottomY < PixelHeight ? _Pixels[bottomY, x] : Color.Default;
                    CellStyle style = new CellStyle(top, bottom);
                    surface.Set(x, cy, Cell.Glyph("▀", style, 1));
                }
            }
        }
    }
}

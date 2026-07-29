namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Content;

    /// <summary>
    /// A widget that draws large block-letter text (see <see cref="Banner"/>) in a single color,
    /// ideal for splash screens and headers. It measures five cells tall and as wide as the rendered
    /// banner.
    /// </summary>
    public sealed class BannerText : IWidget
    {
        private IReadOnlyList<string> _Rows;

        /// <summary>Gets or sets the banner color. Defaults to cyan.</summary>
        public Color Color { get; set; } = Color.FromPalette(6);

        /// <summary>
        /// Initializes a new instance of the <see cref="BannerText"/> class.
        /// </summary>
        /// <param name="text">The text to render as a banner. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public BannerText(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            _Rows = Banner.Render(text);
        }

        /// <summary>
        /// Gets the pixel-free width of the rendered banner, in cells.
        /// </summary>
        public int BannerWidth
        {
            get { return _Rows.Count == 0 ? 0 : _Rows[0].Length; }
        }

        /// <summary>
        /// Replaces the banner text.
        /// </summary>
        /// <param name="text">The new text. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public void SetText(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            _Rows = Banner.Render(text);
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            return new Size(Math.Min(available.Width, BannerWidth), Math.Min(available.Height, _Rows.Count));
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            CellStyle style = CellStyle.Default.WithForeground(Color);
            for (int row = 0; row < _Rows.Count && row < surface.Size.Height; row++)
                surface.DrawText(0, row, _Rows[row], style);
        }
    }
}

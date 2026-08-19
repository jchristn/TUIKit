namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Ascii;
    using TUIKit.Ascii.Fonts;

    /// <summary>
    /// A widget that renders text as multi-row ASCII art in a single color, using any
    /// <see cref="IAsciiFont"/>. The font-aware successor to <see cref="BannerText"/>: it defaults to
    /// the built-in <see cref="BlockAsciiFont"/> and measures as tall as the font and as wide as the
    /// composed art. Set <see cref="Font"/> to any registered or loaded font.
    /// </summary>
    public sealed class AsciiArtText : IWidget
    {
        private IAsciiFont _Font;
        private string _Text;
        private AsciiArtAlignment _Alignment = AsciiArtAlignment.Left;
        private IReadOnlyList<string> _Rows;

        /// <summary>
        /// Gets or sets the art color. Defaults to palette cyan.
        /// </summary>
        public Color Color { get; set; } = Color.FromPalette(6);

        /// <summary>
        /// Gets or sets the font. Must not be null. Defaults to <see cref="BlockAsciiFont"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public IAsciiFont Font
        {
            get { return _Font; }
            set
            {
                _Font = value ?? throw new ArgumentNullException(nameof(value));
                Recompose();
            }
        }

        /// <summary>
        /// Gets or sets the text rendered as art. Must not be null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string Text
        {
            get { return _Text; }
            set
            {
                _Text = value ?? throw new ArgumentNullException(nameof(value));
                Recompose();
            }
        }

        /// <summary>
        /// Gets or sets the horizontal alignment used when the render surface is wider than the art.
        /// Defaults to <see cref="AsciiArtAlignment.Left"/>.
        /// </summary>
        public AsciiArtAlignment Alignment
        {
            get { return _Alignment; }
            set { _Alignment = value; }
        }

        /// <summary>
        /// Gets the width of the composed art in cells.
        /// </summary>
        public int ArtWidth
        {
            get { return _Rows.Count == 0 ? 0 : _Rows[0].Length; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AsciiArtText"/> class using the default block
        /// font.
        /// </summary>
        /// <param name="text">The text to render. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public AsciiArtText(string text)
            : this(text, new BlockAsciiFont())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AsciiArtText"/> class with a font.
        /// </summary>
        /// <param name="text">The text to render. Must not be null.</param>
        /// <param name="font">The font. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> or <paramref name="font"/> is null.</exception>
        public AsciiArtText(string text, IAsciiFont font)
        {
            _Text = text ?? throw new ArgumentNullException(nameof(text));
            _Font = font ?? throw new ArgumentNullException(nameof(font));
            _Rows = AsciiArt.Render(_Text, _Font);
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            int width = Math.Min(available.Width, ArtWidth);
            int height = Math.Min(available.Height, _Rows.Count);
            return new Size(width, height);
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            int width = ArtWidth;
            int available = surface.Size.Width;
            int offset = 0;
            if (available > width)
            {
                if (_Alignment == AsciiArtAlignment.Right)
                    offset = available - width;
                else if (_Alignment == AsciiArtAlignment.Center)
                    offset = (available - width) / 2;
            }

            CellStyle style = CellStyle.Default.WithForeground(Color);
            for (int row = 0; row < _Rows.Count && row < surface.Size.Height; row++)
                surface.DrawText(offset, row, _Rows[row], style);
        }

        private void Recompose()
        {
            _Rows = AsciiArt.Render(_Text, _Font);
        }
    }
}

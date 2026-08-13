namespace TUIKit.Widgets
{
    using System;
    using TUIKit;

    /// <summary>
    /// A thin horizontal or vertical divider line with an optional centered caption. Use it to separate
    /// sections inside a panel or as a light gutter between regions. A horizontal rule occupies one row
    /// and fills the available width; a vertical rule occupies one column and fills the available height.
    /// A caption applies to a horizontal rule only.
    /// </summary>
    public sealed class Rule : IWidget
    {
        private string? _Caption;
        private string _Glyph = "─";
        private string _VerticalGlyph = "│";

        /// <summary>
        /// Initializes a new instance of the <see cref="Rule"/> class as a horizontal rule.
        /// </summary>
        public Rule()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rule"/> class with an orientation.
        /// </summary>
        /// <param name="orientation">The rule orientation.</param>
        public Rule(SplitOrientation orientation)
        {
            Orientation = orientation;
        }

        /// <summary>
        /// Gets or sets the rule orientation. Defaults to <see cref="SplitOrientation.Horizontal"/>.
        /// </summary>
        public SplitOrientation Orientation { get; set; } = SplitOrientation.Horizontal;

        /// <summary>
        /// Gets or sets a caption centered on a horizontal rule, or null for a plain line. Ignored for a
        /// vertical rule. Defaults to null.
        /// </summary>
        public string? Caption
        {
            get { return _Caption; }
            set { _Caption = value; }
        }

        /// <summary>
        /// Gets or sets the glyph repeated to draw a horizontal rule. Must not be null or empty. Defaults to "─".
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when set to null or empty.</exception>
        public string Glyph
        {
            get { return _Glyph; }
            set
            {
                if (string.IsNullOrEmpty(value))
                    throw new ArgumentException("Glyph must not be null or empty.", nameof(value));
                _Glyph = value;
            }
        }

        /// <summary>
        /// Gets or sets the glyph repeated to draw a vertical rule. Must not be null or empty. Defaults to "│".
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when set to null or empty.</exception>
        public string VerticalGlyph
        {
            get { return _VerticalGlyph; }
            set
            {
                if (string.IsNullOrEmpty(value))
                    throw new ArgumentException("Vertical glyph must not be null or empty.", nameof(value));
                _VerticalGlyph = value;
            }
        }

        /// <summary>
        /// Gets or sets the style for the line and caption. Defaults to a dim gray.
        /// </summary>
        public CellStyle Style { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(8));

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            if (Orientation == SplitOrientation.Vertical)
                return new Size(1, available.Height);

            return new Size(available.Width, 1);
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            int width = surface.Size.Width;
            int height = surface.Size.Height;
            if (width <= 0 || height <= 0)
                return;

            if (Orientation == SplitOrientation.Vertical)
            {
                for (int y = 0; y < height; y++)
                    surface.Set(0, y, Cell.Glyph(_VerticalGlyph, Style, 1));

                return;
            }

            for (int x = 0; x < width; x++)
                surface.Set(x, 0, Cell.Glyph(_Glyph, Style, 1));

            if (!string.IsNullOrEmpty(_Caption) && width >= 6)
            {
                string label = " " + _Caption + " ";
                int labelWidth = TUIKit.Unicode.Graphemes.MeasureWidth(label);
                if (labelWidth > width - 2)
                    return;

                int start = Math.Max(1, (width - labelWidth) / 2);
                surface.DrawText(start, 0, label, Style);
            }
        }
    }
}

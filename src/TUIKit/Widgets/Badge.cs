namespace TUIKit.Widgets
{
    using System;
    using TUIKit;
    using TUIKit.Theming;
    using TUIKit.Unicode;

    /// <summary>
    /// A small status label drawn as <c> text </c> with a colored background, for states such as
    /// "Running" or "Failed". The variant picks the color; the text always carries the meaning, so the
    /// badge does not depend on color alone. <see cref="ToStyledText"/> embeds a badge in other text.
    /// </summary>
    /// <remarks>Not thread-safe: use it from the UI loop.</remarks>
    public sealed class Badge : IWidget, IThemeable
    {
        private string _Text;
        private Theme? _Theme;

        /// <summary>
        /// Gets or sets the text. Must not be null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string Text
        {
            get { return _Text; }
            set { _Text = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>
        /// Gets or sets the variant. Defaults to <see cref="BadgeVariant.Neutral"/>.
        /// </summary>
        public BadgeVariant Variant { get; set; }

        /// <summary>
        /// Gets or sets an explicit style that overrides the variant colors, or null to use the variant.
        /// </summary>
        public CellStyle? Style { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Badge"/> class.
        /// </summary>
        /// <param name="text">The text. Must not be null.</param>
        /// <param name="variant">The variant. Defaults to neutral.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public Badge(string text, BadgeVariant variant = BadgeVariant.Neutral)
        {
            _Text = text ?? throw new ArgumentNullException(nameof(text));
            Variant = variant;
        }

        /// <summary>
        /// Gets the rendered width: the text plus one space on each side.
        /// </summary>
        public int Width
        {
            get { return TextFit.Width(_Text) + 2; }
        }

        /// <summary>
        /// Makes the variant colors follow a theme (the variant's role foreground as the background, with
        /// the theme's background as the text color).
        /// </summary>
        /// <param name="theme">The theme. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
        public void ApplyTheme(Theme theme)
        {
            _Theme = theme ?? throw new ArgumentNullException(nameof(theme));
        }

        /// <summary>
        /// Resolves the style used to draw the badge.
        /// </summary>
        /// <returns>The style.</returns>
        public CellStyle ResolveStyle()
        {
            if (Style.HasValue)
                return Style.Value;

            return StyleFor(Variant, _Theme);
        }

        /// <summary>
        /// Gets the badge as styled text, for embedding in a line.
        /// </summary>
        /// <returns>The styled text.</returns>
        public StyledText ToStyledText()
        {
            return TUIKit.Text.From(" " + _Text + " ", ResolveStyle());
        }

        /// <summary>
        /// Gets the style of a variant, from a theme when one is supplied or from palette colors otherwise.
        /// </summary>
        /// <param name="variant">The variant.</param>
        /// <param name="theme">The theme, or null for the palette defaults.</param>
        /// <returns>The style.</returns>
        public static CellStyle StyleFor(BadgeVariant variant, Theme? theme)
        {
            Color black = Color.FromRgb(0, 0, 0);
            if (theme == null)
            {
                byte palette;
                switch (variant)
                {
                    case BadgeVariant.Info: palette = 6; break;
                    case BadgeVariant.Success: palette = 2; break;
                    case BadgeVariant.Warning: palette = 3; break;
                    case BadgeVariant.Error: palette = 1; break;
                    case BadgeVariant.Accent: palette = 5; break;
                    default: palette = 7; break;
                }

                return CellStyle.Default.WithForeground(black).WithBackground(Color.FromPalette(palette));
            }

            CellStyle role;
            switch (variant)
            {
                case BadgeVariant.Info: role = theme.Info; break;
                case BadgeVariant.Success: role = theme.Success; break;
                case BadgeVariant.Warning: role = theme.Warning; break;
                case BadgeVariant.Error: role = theme.Error; break;
                case BadgeVariant.Accent: role = theme.Accent; break;
                default: role = theme.Muted; break;
            }

            return new CellStyle(theme.Text.Background, role.Foreground, CellAttributes.Bold);
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            return new Size(Math.Min(available.Width, Width), available.Height > 0 ? 1 : 0);
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            if (surface.Size.Width <= 0 || surface.Size.Height <= 0)
                return;

            surface.DrawText(0, 0, TextFit.Ellipsize(" " + _Text + " ", surface.Size.Width), ResolveStyle());
        }
    }
}

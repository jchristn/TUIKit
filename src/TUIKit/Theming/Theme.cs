namespace TUIKit.Theming
{
    using System;
    using System.Collections.Generic;
    using TUIKit;

    /// <summary>
    /// A swappable set of styles that widgets consult for their colors, plus named custom styles.
    /// Switching the active theme at runtime and repainting restyles the whole UI. The
    /// <see cref="UseAsciiBorders"/> flag lets a theme fall back to ASCII glyphs on terminals without
    /// box-drawing support.
    /// </summary>
    public sealed class Theme
    {
        private readonly Dictionary<string, CellStyle> _Named = new Dictionary<string, CellStyle>(StringComparer.Ordinal);

        /// <summary>
        /// Gets the theme name. Never null.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the default text style (foreground and background).
        /// </summary>
        public CellStyle Text { get; }

        /// <summary>
        /// Gets the accent style used for emphasis and selection highlights.
        /// </summary>
        public CellStyle Accent { get; }

        /// <summary>
        /// Gets the border style.
        /// </summary>
        public CellStyle Border { get; }

        /// <summary>
        /// Gets the muted style for secondary text.
        /// </summary>
        public CellStyle Muted { get; }

        /// <summary>
        /// Gets a value indicating whether borders should use ASCII glyphs instead of box-drawing.
        /// </summary>
        public bool UseAsciiBorders { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Theme"/> class.
        /// </summary>
        /// <param name="name">The theme name. Must not be null or empty.</param>
        /// <param name="text">The default text style.</param>
        /// <param name="accent">The accent style.</param>
        /// <param name="border">The border style.</param>
        /// <param name="muted">The muted style.</param>
        /// <param name="useAsciiBorders">Whether to use ASCII borders.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or empty.</exception>
        public Theme(string name, CellStyle text, CellStyle accent, CellStyle border, CellStyle muted, bool useAsciiBorders = false)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Theme name must not be null or empty.", nameof(name));

            Name = name;
            Text = text;
            Accent = accent;
            Border = border;
            Muted = muted;
            UseAsciiBorders = useAsciiBorders;
        }

        /// <summary>
        /// Gets a dark theme.
        /// </summary>
        public static Theme Dark
        {
            get
            {
                return new Theme(
                    "Dark",
                    CellStyle.Default.WithForeground(Color.FromRgb(0xD0, 0xD0, 0xD0)),
                    CellStyle.Default.WithForeground(Color.FromRgb(0x4F, 0xC1, 0xE9)),
                    CellStyle.Default.WithForeground(Color.FromRgb(0x50, 0x50, 0x60)),
                    CellStyle.Default.WithForeground(Color.FromRgb(0x80, 0x80, 0x90)));
            }
        }

        /// <summary>
        /// Gets a light theme.
        /// </summary>
        public static Theme Light
        {
            get
            {
                return new Theme(
                    "Light",
                    CellStyle.Default.WithForeground(Color.FromRgb(0x20, 0x20, 0x20)),
                    CellStyle.Default.WithForeground(Color.FromRgb(0x0A, 0x6C, 0xB0)),
                    CellStyle.Default.WithForeground(Color.FromRgb(0xB0, 0xB0, 0xB0)),
                    CellStyle.Default.WithForeground(Color.FromRgb(0x60, 0x60, 0x60)));
            }
        }

        /// <summary>
        /// Gets a high-contrast theme that also uses ASCII borders.
        /// </summary>
        public static Theme HighContrast
        {
            get
            {
                return new Theme(
                    "HighContrast",
                    CellStyle.Default.WithForeground(Color.FromRgb(0xFF, 0xFF, 0xFF)),
                    CellStyle.Default.WithForeground(Color.FromRgb(0xFF, 0xFF, 0x00)),
                    CellStyle.Default.WithForeground(Color.FromRgb(0xFF, 0xFF, 0xFF)),
                    CellStyle.Default.WithForeground(Color.FromRgb(0xC0, 0xC0, 0xC0)),
                    true);
            }
        }

        /// <summary>
        /// Registers or replaces a named style.
        /// </summary>
        /// <param name="name">The style name. Must not be null or empty.</param>
        /// <param name="style">The style.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or empty.</exception>
        public void SetStyle(string name, CellStyle style)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Style name must not be null or empty.", nameof(name));

            _Named[name] = style;
        }

        /// <summary>
        /// Gets a named style, or the default text style when it is not defined.
        /// </summary>
        /// <param name="name">The style name. Must not be null.</param>
        /// <returns>The style.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
        public CellStyle GetStyle(string name)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            return _Named.TryGetValue(name, out CellStyle style) ? style : Text;
        }
    }
}

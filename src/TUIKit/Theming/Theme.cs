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
    /// <remarks>
    /// Named styles registered with <see cref="SetStyle"/> double as background roles for layout
    /// regions: a region built with <c>BackgroundRole("name")</c> paints the background of the style
    /// registered under that name, so a theme switch can restyle panels without any code change. The
    /// built-in presets register the conventional roles named by <see cref="SidebarRole"/> and
    /// <see cref="StatusBarRole"/>.
    /// </remarks>
    public sealed class Theme
    {
        /// <summary>
        /// Gets the conventional theme-style name for a sidebar panel background. The built-in presets
        /// register a style under this name; regions can bind to it with <c>BackgroundRole</c>.
        /// </summary>
        public static string SidebarRole
        {
            get { return "sidebar"; }
        }

        /// <summary>
        /// Gets the conventional theme-style name for a status-bar or footer background. The built-in
        /// presets register a style under this name; regions can bind to it with <c>BackgroundRole</c>.
        /// </summary>
        public static string StatusBarRole
        {
            get { return "statusbar"; }
        }

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
        /// Gets the style for successful or positive states (green foreground on the theme background).
        /// </summary>
        public CellStyle Success { get; }

        /// <summary>
        /// Gets the style for warnings (yellow foreground on the theme background).
        /// </summary>
        public CellStyle Warning { get; }

        /// <summary>
        /// Gets the style for errors (red foreground on the theme background).
        /// </summary>
        public CellStyle Error { get; }

        /// <summary>
        /// Gets the style for informational states (cyan foreground on the theme background).
        /// </summary>
        public CellStyle Info { get; }

        /// <summary>
        /// Gets the style for the selected/highlighted item (reversed against the accent color).
        /// </summary>
        public CellStyle Selection { get; }

        /// <summary>
        /// Gets the style for disabled or unavailable content (dimmed).
        /// </summary>
        public CellStyle Disabled { get; }

        /// <summary>
        /// Gets a value indicating whether borders should use ASCII glyphs instead of box-drawing.
        /// </summary>
        public bool UseAsciiBorders { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Theme"/> class. The semantic role styles
        /// (success, warning, error, info, selection, disabled) default to sensible colors derived
        /// from the text background when not supplied.
        /// </summary>
        /// <param name="name">The theme name. Must not be null or empty.</param>
        /// <param name="text">The default text style.</param>
        /// <param name="accent">The accent style.</param>
        /// <param name="border">The border style.</param>
        /// <param name="muted">The muted style.</param>
        /// <param name="useAsciiBorders">Whether to use ASCII borders. Defaults to false.</param>
        /// <param name="success">The success style, or null to derive it.</param>
        /// <param name="warning">The warning style, or null to derive it.</param>
        /// <param name="error">The error style, or null to derive it.</param>
        /// <param name="info">The info style, or null to derive it.</param>
        /// <param name="selection">The selection style, or null to derive it.</param>
        /// <param name="disabled">The disabled style, or null to derive it.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or empty.</exception>
        public Theme(
            string name,
            CellStyle text,
            CellStyle accent,
            CellStyle border,
            CellStyle muted,
            bool useAsciiBorders = false,
            CellStyle? success = null,
            CellStyle? warning = null,
            CellStyle? error = null,
            CellStyle? info = null,
            CellStyle? selection = null,
            CellStyle? disabled = null)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Theme name must not be null or empty.", nameof(name));

            Name = name;
            Text = text;
            Accent = accent;
            Border = border;
            Muted = muted;
            UseAsciiBorders = useAsciiBorders;

            Color background = text.Background;
            Success = success ?? Role(Color.FromPalette(2), background);
            Warning = warning ?? Role(Color.FromPalette(3), background);
            Error = error ?? Role(Color.FromPalette(1), background);
            Info = info ?? Role(Color.FromPalette(6), background);
            Selection = selection ?? new CellStyle(text.Background, accent.Foreground);
            Disabled = disabled ?? muted.WithAttribute(CellAttributes.Dim, true);
        }

        private static CellStyle Role(Color foreground, Color background)
        {
            return new CellStyle(foreground, background);
        }

        /// <summary>
        /// Gets a dark theme: light text on a dark background.
        /// </summary>
        public static Theme Dark
        {
            get
            {
                Color background = Color.FromRgb(0x1E, 0x1E, 0x1E);
                Theme theme = new Theme(
                    "Dark",
                    Styled(Color.FromRgb(0xD0, 0xD0, 0xD0), background),
                    Styled(Color.FromRgb(0x4F, 0xC1, 0xE9), background),
                    Styled(Color.FromRgb(0x5A, 0x5A, 0x6A), background),
                    Styled(Color.FromRgb(0x8A, 0x8A, 0x9A), background));
                return WithConventionalRoles(theme, Color.FromRgb(0x18, 0x18, 0x1B), Color.FromRgb(0x2D, 0x2D, 0x30));
            }
        }

        /// <summary>
        /// Gets a light theme: dark text on a light background (the reverse of <see cref="Dark"/>).
        /// </summary>
        public static Theme Light
        {
            get
            {
                Color background = Color.FromRgb(0xE8, 0xE8, 0xE8);
                Theme theme = new Theme(
                    "Light",
                    Styled(Color.FromRgb(0x20, 0x20, 0x20), background),
                    Styled(Color.FromRgb(0x0A, 0x5C, 0xA0), background),
                    Styled(Color.FromRgb(0x90, 0x90, 0x90), background),
                    Styled(Color.FromRgb(0x55, 0x55, 0x55), background));
                return WithConventionalRoles(theme, Color.FromRgb(0xD8, 0xD8, 0xD8), Color.FromRgb(0xCE, 0xCE, 0xCE));
            }
        }

        /// <summary>
        /// Gets a high-contrast theme (white on black) that also uses ASCII borders.
        /// </summary>
        public static Theme HighContrast
        {
            get
            {
                Color background = Color.FromRgb(0x00, 0x00, 0x00);
                Theme theme = new Theme(
                    "HighContrast",
                    Styled(Color.FromRgb(0xFF, 0xFF, 0xFF), background),
                    Styled(Color.FromRgb(0xFF, 0xFF, 0x00), background),
                    Styled(Color.FromRgb(0xFF, 0xFF, 0xFF), background),
                    Styled(Color.FromRgb(0xC0, 0xC0, 0xC0), background),
                    true);
                return WithConventionalRoles(theme, Color.FromRgb(0x00, 0x00, 0x00), Color.FromRgb(0x00, 0x00, 0x00));
            }
        }

        private static CellStyle Styled(Color foreground, Color background)
        {
            return CellStyle.Default.WithForeground(foreground).WithBackground(background);
        }

        private static Theme WithConventionalRoles(Theme theme, Color sidebar, Color statusBar)
        {
            theme.SetStyle(SidebarRole, theme.Text.WithBackground(sidebar));
            theme.SetStyle(StatusBarRole, theme.Text.WithBackground(statusBar));
            return theme;
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

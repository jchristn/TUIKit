namespace TUIKit.Content
{
    using System;
    using TUIKit;
    using TUIKit.Theming;

    /// <summary>
    /// The styles <see cref="MarkdownRenderer"/> uses for each Markdown element. <see cref="Default"/>
    /// reproduces the renderer's original palette colors; <see cref="FromTheme"/> derives the styles from a
    /// <see cref="Theme"/> so Markdown follows light, dark, and high-contrast themes. Element styles are
    /// overlaid on the base text style: a channel left at the default color inherits the base, and
    /// attributes combine. Instances are mutable; do not mutate one while another thread renders with it.
    /// </summary>
    public sealed class MarkdownStyles
    {
        /// <summary>
        /// Gets or sets the style of plain text. Defaults to <see cref="CellStyle.Default"/>.
        /// </summary>
        public CellStyle Text { get; set; } = CellStyle.Default;

        /// <summary>
        /// Gets or sets the style of level-1 headings. Defaults to bold cyan (palette 6).
        /// </summary>
        public CellStyle Heading1 { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(6)).WithAttribute(CellAttributes.Bold, true);

        /// <summary>
        /// Gets or sets the style of level-2 and deeper headings. Defaults to bold blue (palette 4).
        /// </summary>
        public CellStyle Heading { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(4)).WithAttribute(CellAttributes.Bold, true);

        /// <summary>
        /// Gets or sets the style of fenced code block lines. Defaults to green (palette 2).
        /// </summary>
        public CellStyle CodeBlock { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(2));

        /// <summary>
        /// Gets or sets the style overlaid on inline <c>`code`</c> spans. Defaults to yellow (palette 3).
        /// </summary>
        public CellStyle InlineCode { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(3));

        /// <summary>
        /// Gets or sets the style of list bullets and ordered-list markers. Defaults to cyan (palette 6).
        /// </summary>
        public CellStyle Bullet { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(6));

        /// <summary>
        /// Gets or sets the style of block quotes. Defaults to dim text.
        /// </summary>
        public CellStyle Quote { get; set; } = CellStyle.Default.WithAttribute(CellAttributes.Dim, true);

        /// <summary>
        /// Gets or sets the style of horizontal rules and table separators. Defaults to grey (palette 8).
        /// </summary>
        public CellStyle Rule { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(8));

        /// <summary>
        /// Gets or sets the style of an open task marker. Defaults to cyan (palette 6).
        /// </summary>
        public CellStyle TaskOpen { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(6));

        /// <summary>
        /// Gets or sets the style of a completed task marker. Defaults to green (palette 2).
        /// </summary>
        public CellStyle TaskDone { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(2));

        /// <summary>
        /// Gets a new instance holding the renderer's original colors. Each call returns a fresh copy.
        /// </summary>
        public static MarkdownStyles Default
        {
            get { return new MarkdownStyles(); }
        }

        /// <summary>
        /// Derives Markdown styles from a theme: text from <see cref="Theme.Text"/>, headings from
        /// <see cref="Theme.Accent"/> and <see cref="Theme.Info"/>, code from <see cref="Theme.Success"/>
        /// and <see cref="Theme.Warning"/>, quotes and rules from <see cref="Theme.Muted"/> and
        /// <see cref="Theme.Border"/>. A theme can override any element with a named style registered under
        /// the role names in <see cref="Theme"/> (for example <see cref="Theme.MarkdownCodeRole"/>).
        /// </summary>
        /// <param name="theme">The theme. Must not be null.</param>
        /// <returns>A new styles instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
        public static MarkdownStyles FromTheme(Theme theme)
        {
            if (theme == null)
                throw new ArgumentNullException(nameof(theme));

            MarkdownStyles styles = new MarkdownStyles();
            styles.Text = theme.Text;
            styles.Heading1 = theme.Resolve(Theme.MarkdownHeadingRole, theme.Accent.WithAttribute(CellAttributes.Bold, true));
            styles.Heading = theme.Resolve(Theme.MarkdownSubheadingRole, theme.Info.WithAttribute(CellAttributes.Bold, true));
            styles.CodeBlock = theme.Resolve(Theme.MarkdownCodeRole, theme.Success);
            styles.InlineCode = theme.Resolve(Theme.MarkdownInlineCodeRole, theme.Warning);
            styles.Bullet = theme.Accent;
            styles.Quote = theme.Resolve(Theme.MarkdownQuoteRole, theme.Muted);
            styles.Rule = theme.Border;
            styles.TaskOpen = theme.Accent;
            styles.TaskDone = theme.Success;
            return styles;
        }

        /// <summary>
        /// Overlays an element style on a base style: colors left at the default inherit the base and the
        /// attributes of both combine.
        /// </summary>
        /// <param name="baseStyle">The base style.</param>
        /// <param name="overlay">The element style.</param>
        /// <returns>The combined style.</returns>
        public static CellStyle Overlay(CellStyle baseStyle, CellStyle overlay)
        {
            Color foreground = overlay.Foreground.Kind != ColorKind.Default ? overlay.Foreground : baseStyle.Foreground;
            Color background = overlay.Background.Kind != ColorKind.Default ? overlay.Background : baseStyle.Background;
            return new CellStyle(foreground, background, baseStyle.Attributes | overlay.Attributes, baseStyle.LinkId);
        }
    }
}

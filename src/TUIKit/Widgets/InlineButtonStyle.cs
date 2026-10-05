namespace TUIKit.Widgets
{
    using System;
    using TUIKit;
    using TUIKit.Theming;

    /// <summary>
    /// The styles <see cref="InlineButton"/> draws with: the bracketed label, the key hint after it, and
    /// the label while the pointer is over it. Every hover state also adds underline, so it never relies
    /// on color alone. Not thread-safe; configure on the UI thread.
    /// </summary>
    public sealed class InlineButtonStyle
    {
        /// <summary>
        /// Gets or sets the style of the bracketed label. Defaults to bold cyan (palette 6).
        /// </summary>
        public CellStyle Label { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(6)).WithAttribute(CellAttributes.Bold, true);

        /// <summary>
        /// Gets or sets the style of the key hint after the label. Defaults to gray (palette 8).
        /// </summary>
        public CellStyle Key { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(8));

        /// <summary>
        /// Gets or sets the style of the label while the pointer is over the button. Defaults to bold,
        /// underlined cyan.
        /// </summary>
        public CellStyle Hover { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(6)).WithAttribute(CellAttributes.Bold, true).WithAttribute(CellAttributes.Underline, true);

        /// <summary>
        /// Builds a style set from a theme: the label in bold <see cref="Theme.Accent"/>, the key in
        /// <see cref="Theme.Muted"/>, and hover from <see cref="Theme.InlineButtonHoverRole"/> (falling back
        /// to the label style underlined).
        /// </summary>
        /// <param name="theme">The theme. Must not be null.</param>
        /// <returns>A new style set. Never null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
        public static InlineButtonStyle FromTheme(Theme theme)
        {
            if (theme == null)
                throw new ArgumentNullException(nameof(theme));

            CellStyle label = theme.Accent.WithAttribute(CellAttributes.Bold, true);
            return new InlineButtonStyle
            {
                Label = label,
                Key = theme.Muted,
                Hover = theme.Resolve(Theme.InlineButtonHoverRole, label).WithAttribute(CellAttributes.Underline, true)
            };
        }
    }
}

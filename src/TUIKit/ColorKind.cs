namespace TUIKit
{
    /// <summary>
    /// Identifies how a <see cref="Color"/> value should be interpreted when emitted to a terminal.
    /// </summary>
    public enum ColorKind
    {
        /// <summary>
        /// The terminal's default foreground or background color. No explicit color is emitted.
        /// </summary>
        Default = 0,

        /// <summary>
        /// An index into the terminal's 256-color palette (0 through 255).
        /// </summary>
        Palette = 1,

        /// <summary>
        /// A 24-bit truecolor value with explicit red, green, and blue channels.
        /// </summary>
        Rgb = 2
    }
}

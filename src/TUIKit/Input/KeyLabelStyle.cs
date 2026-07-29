namespace TUIKit.Input
{
    /// <summary>
    /// Selects how a <see cref="KeyChord"/> is rendered for display in help text, footers, and menus.
    /// The appropriate style depends on the operating environment; see <see cref="KeyLabel.Recommended"/>.
    /// </summary>
    public enum KeyLabelStyle
    {
        /// <summary>
        /// Spelled-out ASCII labels joined with <c>+</c>, for example <c>Ctrl+G</c>, <c>Alt+Enter</c>,
        /// or <c>PgUp</c>. The conventional style on Windows and Linux.
        /// </summary>
        Ascii = 0,

        /// <summary>
        /// Compact glyph labels using the platform modifier symbols and no separator, for example
        /// <c>⌃G</c>, <c>⌥⏎</c>, or <c>⇞</c>. The conventional style on macOS.
        /// </summary>
        Symbols = 1
    }
}

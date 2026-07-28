namespace TUIKit.Terminal
{
    /// <summary>
    /// The color fidelity a terminal can render. The renderer degrades truecolor output to the
    /// available depth at emit time.
    /// </summary>
    public enum TerminalColorDepth
    {
        /// <summary>
        /// No color support; only the default foreground and background are used.
        /// </summary>
        None = 0,

        /// <summary>
        /// The 16-color ANSI palette.
        /// </summary>
        Ansi16 = 1,

        /// <summary>
        /// The 256-color palette.
        /// </summary>
        Palette256 = 2,

        /// <summary>
        /// 24-bit truecolor.
        /// </summary>
        TrueColor = 3
    }
}

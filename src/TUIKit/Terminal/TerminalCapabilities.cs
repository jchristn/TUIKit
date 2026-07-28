namespace TUIKit.Terminal
{
    /// <summary>
    /// Describes the features a terminal backend supports. A backend reports its capabilities so the
    /// renderer and input layers can adapt (degrade color, choose a keyboard protocol, decide whether
    /// hyperlinks or clipboard escapes are usable). Instances are immutable once constructed.
    /// </summary>
    public sealed class TerminalCapabilities
    {
        /// <summary>
        /// Gets the color depth the terminal can render. Defaults to <see cref="TerminalColorDepth.TrueColor"/>.
        /// </summary>
        public TerminalColorDepth ColorDepth { get; }

        /// <summary>
        /// Gets a value indicating whether the terminal supports an enhanced keyboard protocol
        /// (Kitty / CSI u or win32-input-mode) that disambiguates keys and modifiers.
        /// </summary>
        public bool EnhancedKeyboard { get; }

        /// <summary>
        /// Gets a value indicating whether the terminal supports SGR (1006) extended mouse reporting,
        /// which is required for coordinates beyond column 223.
        /// </summary>
        public bool SgrMouse { get; }

        /// <summary>
        /// Gets a value indicating whether the terminal honors OSC 8 hyperlinks.
        /// </summary>
        public bool Hyperlinks { get; }

        /// <summary>
        /// Gets a value indicating whether the terminal supports OSC 52 clipboard access, which works
        /// over SSH.
        /// </summary>
        public bool ClipboardOsc52 { get; }

        /// <summary>
        /// Gets a value indicating whether the terminal supports bracketed paste (mode 2004).
        /// </summary>
        public bool BracketedPaste { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TerminalCapabilities"/> class.
        /// </summary>
        /// <param name="colorDepth">The color depth the terminal can render.</param>
        /// <param name="enhancedKeyboard">Whether an enhanced keyboard protocol is available.</param>
        /// <param name="sgrMouse">Whether SGR mouse reporting is available.</param>
        /// <param name="hyperlinks">Whether OSC 8 hyperlinks are honored.</param>
        /// <param name="clipboardOsc52">Whether OSC 52 clipboard access is available.</param>
        /// <param name="bracketedPaste">Whether bracketed paste is available.</param>
        public TerminalCapabilities(
            TerminalColorDepth colorDepth,
            bool enhancedKeyboard,
            bool sgrMouse,
            bool hyperlinks,
            bool clipboardOsc52,
            bool bracketedPaste)
        {
            ColorDepth = colorDepth;
            EnhancedKeyboard = enhancedKeyboard;
            SgrMouse = sgrMouse;
            Hyperlinks = hyperlinks;
            ClipboardOsc52 = clipboardOsc52;
            BracketedPaste = bracketedPaste;
        }

        /// <summary>
        /// Gets a fully featured capability set (truecolor, enhanced keyboard, SGR mouse, hyperlinks,
        /// OSC 52, bracketed paste). Useful for headless rendering and modern tier-1 terminals.
        /// </summary>
        public static TerminalCapabilities Full
        {
            get { return new TerminalCapabilities(TerminalColorDepth.TrueColor, true, true, true, true, true); }
        }

        /// <summary>
        /// Gets a minimal capability set (16 colors, no enhanced input, no mouse, no clipboard),
        /// representing a degraded or legacy terminal.
        /// </summary>
        public static TerminalCapabilities Minimal
        {
            get { return new TerminalCapabilities(TerminalColorDepth.Ansi16, false, false, false, false, false); }
        }
    }
}

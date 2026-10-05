namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "RubiFont" FIGlet font (registered as "RubiFont"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class RubiFontAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RubiFontAsciiFont"/> class.
        /// </summary>
        public RubiFontAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.RubiFont.flf", "RubiFont")
        {
        }
    }
}

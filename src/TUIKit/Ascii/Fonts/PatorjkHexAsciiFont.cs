namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Patorjk-HeX" FIGlet font (registered as "PatorjkHex"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class PatorjkHexAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PatorjkHexAsciiFont"/> class.
        /// </summary>
        public PatorjkHexAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.PatorjkHex.flf", "PatorjkHex")
        {
        }
    }
}

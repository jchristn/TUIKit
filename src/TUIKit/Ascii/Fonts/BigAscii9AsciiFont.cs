namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Big ASCII 9" FIGlet font (registered as "BigAscii9"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class BigAscii9AsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BigAscii9AsciiFont"/> class.
        /// </summary>
        public BigAscii9AsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.BigAscii9.flf", "BigAscii9")
        {
        }
    }
}

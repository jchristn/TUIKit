namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Big ASCII 12" FIGlet font (registered as "BigAscii12"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class BigAscii12AsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BigAscii12AsciiFont"/> class.
        /// </summary>
        public BigAscii12AsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.BigAscii12.flf", "BigAscii12")
        {
        }
    }
}

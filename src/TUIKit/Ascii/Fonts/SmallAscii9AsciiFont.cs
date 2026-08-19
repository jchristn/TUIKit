namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Small ASCII 9" FIGlet font (registered as "SmallAscii9"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class SmallAscii9AsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SmallAscii9AsciiFont"/> class.
        /// </summary>
        public SmallAscii9AsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.SmallAscii9.flf", "SmallAscii9")
        {
        }
    }
}

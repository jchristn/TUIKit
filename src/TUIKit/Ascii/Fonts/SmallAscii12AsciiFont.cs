namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Small ASCII 12" FIGlet font (registered as "SmallAscii12"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class SmallAscii12AsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SmallAscii12AsciiFont"/> class.
        /// </summary>
        public SmallAscii12AsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.SmallAscii12.flf", "SmallAscii12")
        {
        }
    }
}

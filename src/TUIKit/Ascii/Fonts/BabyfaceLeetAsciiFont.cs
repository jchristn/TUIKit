namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Babyface Leet" FIGlet font (registered as "BabyfaceLeet"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class BabyfaceLeetAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BabyfaceLeetAsciiFont"/> class.
        /// </summary>
        public BabyfaceLeetAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.BabyfaceLeet.flf", "BabyfaceLeet")
        {
        }
    }
}

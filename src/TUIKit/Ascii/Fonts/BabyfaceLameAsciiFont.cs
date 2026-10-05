namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Babyface Lame" FIGlet font (registered as "BabyfaceLame"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class BabyfaceLameAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BabyfaceLameAsciiFont"/> class.
        /// </summary>
        public BabyfaceLameAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.BabyfaceLame.flf", "BabyfaceLame")
        {
        }
    }
}

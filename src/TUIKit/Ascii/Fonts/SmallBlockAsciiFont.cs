namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Small Block" FIGlet font (registered as "SmallBlock"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class SmallBlockAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SmallBlockAsciiFont"/> class.
        /// </summary>
        public SmallBlockAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.SmallBlock.flf", "SmallBlock")
        {
        }
    }
}

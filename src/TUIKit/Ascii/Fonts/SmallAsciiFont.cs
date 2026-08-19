namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Small" FIGlet font (registered as "Small"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class SmallAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SmallAsciiFont"/> class.
        /// </summary>
        public SmallAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Small.flf", "Small")
        {
        }
    }
}

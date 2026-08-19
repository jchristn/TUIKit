namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Banner3" FIGlet font (registered as "Banner3"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class Banner3AsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Banner3AsciiFont"/> class.
        /// </summary>
        public Banner3AsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Banner3.flf", "Banner3")
        {
        }
    }
}

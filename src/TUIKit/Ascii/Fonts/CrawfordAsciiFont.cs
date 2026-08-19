namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Crawford" FIGlet font (registered as "Crawford"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class CrawfordAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CrawfordAsciiFont"/> class.
        /// </summary>
        public CrawfordAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Crawford.flf", "Crawford")
        {
        }
    }
}

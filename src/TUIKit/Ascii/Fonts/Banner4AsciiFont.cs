namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Banner4" FIGlet font (registered as "Banner4"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class Banner4AsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Banner4AsciiFont"/> class.
        /// </summary>
        public Banner4AsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Banner4.flf", "Banner4")
        {
        }
    }
}

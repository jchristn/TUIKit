namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Dancing Font" FIGlet font (registered as "DancingFont"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class DancingFontAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DancingFontAsciiFont"/> class.
        /// </summary>
        public DancingFontAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.DancingFont.flf", "DancingFont")
        {
        }
    }
}

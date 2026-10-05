namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "DiamFont" FIGlet font (registered as "DiamFont"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class DiamFontAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DiamFontAsciiFont"/> class.
        /// </summary>
        public DiamFontAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.DiamFont.flf", "DiamFont")
        {
        }
    }
}

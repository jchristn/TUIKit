namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Fire Font-s" FIGlet font (registered as "FireFontS"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class FireFontSAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FireFontSAsciiFont"/> class.
        /// </summary>
        public FireFontSAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.FireFontS.flf", "FireFontS")
        {
        }
    }
}

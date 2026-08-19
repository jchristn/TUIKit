namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Fire Font-k" FIGlet font (registered as "FireFontK"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class FireFontKAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FireFontKAsciiFont"/> class.
        /// </summary>
        public FireFontKAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.FireFontK.flf", "FireFontK")
        {
        }
    }
}

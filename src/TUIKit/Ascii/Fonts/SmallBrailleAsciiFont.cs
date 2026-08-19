namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Small Braille" FIGlet font (registered as "SmallBraille"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class SmallBrailleAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SmallBrailleAsciiFont"/> class.
        /// </summary>
        public SmallBrailleAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.SmallBraille.flf", "SmallBraille")
        {
        }
    }
}

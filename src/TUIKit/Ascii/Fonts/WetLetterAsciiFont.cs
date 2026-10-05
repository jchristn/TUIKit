namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Wet Letter" FIGlet font (registered as "WetLetter"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class WetLetterAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WetLetterAsciiFont"/> class.
        /// </summary>
        public WetLetterAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.WetLetter.flf", "WetLetter")
        {
        }
    }
}

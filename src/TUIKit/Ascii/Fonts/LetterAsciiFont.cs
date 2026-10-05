namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Letter" FIGlet font (registered as "Letter"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class LetterAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LetterAsciiFont"/> class.
        /// </summary>
        public LetterAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Letter.flf", "Letter")
        {
        }
    }
}

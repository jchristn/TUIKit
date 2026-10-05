namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "JS Bracket Letters" FIGlet font (registered as "JsBracketLetters"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class JsBracketLettersAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="JsBracketLettersAsciiFont"/> class.
        /// </summary>
        public JsBracketLettersAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.JsBracketLetters.flf", "JsBracketLetters")
        {
        }
    }
}

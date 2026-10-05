namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Cards" FIGlet font (registered as "Cards"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class CardsAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CardsAsciiFont"/> class.
        /// </summary>
        public CardsAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Cards.flf", "Cards")
        {
        }
    }
}

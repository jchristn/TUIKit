namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Star Wars" FIGlet font (registered as "StarWars"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class StarWarsAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StarWarsAsciiFont"/> class.
        /// </summary>
        public StarWarsAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.StarWars.flf", "StarWars")
        {
        }
    }
}

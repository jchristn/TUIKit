namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Patorjk's Cheese" FIGlet font (registered as "PatorjksCheese"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class PatorjksCheeseAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PatorjksCheeseAsciiFont"/> class.
        /// </summary>
        public PatorjksCheeseAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.PatorjksCheese.flf", "PatorjksCheese")
        {
        }
    }
}

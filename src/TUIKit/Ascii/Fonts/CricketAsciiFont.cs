namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Cricket" FIGlet font (registered as "Cricket"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class CricketAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CricketAsciiFont"/> class.
        /// </summary>
        public CricketAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Cricket.flf", "Cricket")
        {
        }
    }
}

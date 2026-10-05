namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Stronger Than All" FIGlet font (registered as "StrongerThanAll"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class StrongerThanAllAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StrongerThanAllAsciiFont"/> class.
        /// </summary>
        public StrongerThanAllAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.StrongerThanAll.flf", "StrongerThanAll")
        {
        }
    }
}

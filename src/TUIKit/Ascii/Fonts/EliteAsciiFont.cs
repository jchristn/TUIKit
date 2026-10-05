namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Elite" FIGlet font (registered as "Elite"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class EliteAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EliteAsciiFont"/> class.
        /// </summary>
        public EliteAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Elite.flf", "Elite")
        {
        }
    }
}

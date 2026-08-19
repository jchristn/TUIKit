namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Caligraphy" FIGlet font (registered as "Caligraphy"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class CaligraphyAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CaligraphyAsciiFont"/> class.
        /// </summary>
        public CaligraphyAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Caligraphy.flf", "Caligraphy")
        {
        }
    }
}

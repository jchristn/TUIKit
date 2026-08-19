namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Calvin S" FIGlet font (registered as "CalvinS"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class CalvinSAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CalvinSAsciiFont"/> class.
        /// </summary>
        public CalvinSAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.CalvinS.flf", "CalvinS")
        {
        }
    }
}

namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Cybermedium" FIGlet font (registered as "Cybermedium"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class CybermediumAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CybermediumAsciiFont"/> class.
        /// </summary>
        public CybermediumAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Cybermedium.flf", "Cybermedium")
        {
        }
    }
}

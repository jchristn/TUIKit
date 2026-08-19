namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Cyberlarge" FIGlet font (registered as "Cyberlarge"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class CyberlargeAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CyberlargeAsciiFont"/> class.
        /// </summary>
        public CyberlargeAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Cyberlarge.flf", "Cyberlarge")
        {
        }
    }
}

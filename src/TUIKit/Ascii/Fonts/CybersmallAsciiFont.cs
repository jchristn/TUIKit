namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Cybersmall" FIGlet font (registered as "Cybersmall"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class CybersmallAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CybersmallAsciiFont"/> class.
        /// </summary>
        public CybersmallAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Cybersmall.flf", "Cybersmall")
        {
        }
    }
}

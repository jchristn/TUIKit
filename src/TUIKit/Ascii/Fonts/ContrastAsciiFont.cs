namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Contrast" FIGlet font (registered as "Contrast"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class ContrastAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ContrastAsciiFont"/> class.
        /// </summary>
        public ContrastAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Contrast.flf", "Contrast")
        {
        }
    }
}

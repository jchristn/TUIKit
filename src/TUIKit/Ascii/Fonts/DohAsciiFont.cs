namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Doh" FIGlet font (registered as "Doh"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class DohAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DohAsciiFont"/> class.
        /// </summary>
        public DohAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Doh.flf", "Doh")
        {
        }
    }
}

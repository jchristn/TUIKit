namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Pagga" FIGlet font (registered as "Pagga"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class PaggaAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PaggaAsciiFont"/> class.
        /// </summary>
        public PaggaAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Pagga.flf", "Pagga")
        {
        }
    }
}

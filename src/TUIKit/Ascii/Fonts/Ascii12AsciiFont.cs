namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "ASCII 12" FIGlet font (registered as "Ascii12"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class Ascii12AsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Ascii12AsciiFont"/> class.
        /// </summary>
        public Ascii12AsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Ascii12.flf", "Ascii12")
        {
        }
    }
}

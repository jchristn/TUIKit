namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Emboss 2" FIGlet font (registered as "Emboss2"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class Emboss2AsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Emboss2AsciiFont"/> class.
        /// </summary>
        public Emboss2AsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Emboss2.flf", "Emboss2")
        {
        }
    }
}

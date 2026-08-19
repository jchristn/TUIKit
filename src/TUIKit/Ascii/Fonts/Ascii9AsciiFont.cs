namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "ASCII 9" FIGlet font (registered as "Ascii9"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class Ascii9AsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Ascii9AsciiFont"/> class.
        /// </summary>
        public Ascii9AsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Ascii9.flf", "Ascii9")
        {
        }
    }
}

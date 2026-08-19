namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Mono 9" FIGlet font (registered as "Mono9"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class Mono9AsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Mono9AsciiFont"/> class.
        /// </summary>
        public Mono9AsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Mono9.flf", "Mono9")
        {
        }
    }
}

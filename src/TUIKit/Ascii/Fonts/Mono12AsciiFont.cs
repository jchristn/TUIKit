namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Mono 12" FIGlet font (registered as "Mono12"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class Mono12AsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Mono12AsciiFont"/> class.
        /// </summary>
        public Mono12AsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Mono12.flf", "Mono12")
        {
        }
    }
}

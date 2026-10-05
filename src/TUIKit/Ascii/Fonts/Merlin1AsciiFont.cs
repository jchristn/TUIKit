namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Merlin1" FIGlet font (registered as "Merlin1"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class Merlin1AsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Merlin1AsciiFont"/> class.
        /// </summary>
        public Merlin1AsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Merlin1.flf", "Merlin1")
        {
        }
    }
}

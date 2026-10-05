namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Isometric1" FIGlet font (registered as "Isometric1"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class Isometric1AsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Isometric1AsciiFont"/> class.
        /// </summary>
        public Isometric1AsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Isometric1.flf", "Isometric1")
        {
        }
    }
}

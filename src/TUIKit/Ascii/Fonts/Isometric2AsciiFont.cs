namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Isometric2" FIGlet font (registered as "Isometric2"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class Isometric2AsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Isometric2AsciiFont"/> class.
        /// </summary>
        public Isometric2AsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Isometric2.flf", "Isometric2")
        {
        }
    }
}

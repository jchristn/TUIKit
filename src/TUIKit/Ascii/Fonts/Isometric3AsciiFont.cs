namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Isometric3" FIGlet font (registered as "Isometric3"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class Isometric3AsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Isometric3AsciiFont"/> class.
        /// </summary>
        public Isometric3AsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Isometric3.flf", "Isometric3")
        {
        }
    }
}

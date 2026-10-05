namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Isometric4" FIGlet font (registered as "Isometric4"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class Isometric4AsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Isometric4AsciiFont"/> class.
        /// </summary>
        public Isometric4AsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Isometric4.flf", "Isometric4")
        {
        }
    }
}

namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Rectangles" FIGlet font (registered as "Rectangles"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class RectanglesAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RectanglesAsciiFont"/> class.
        /// </summary>
        public RectanglesAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Rectangles.flf", "Rectangles")
        {
        }
    }
}

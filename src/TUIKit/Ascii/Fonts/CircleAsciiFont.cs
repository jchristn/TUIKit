namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Circle" FIGlet font (registered as "Circle"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class CircleAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CircleAsciiFont"/> class.
        /// </summary>
        public CircleAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Circle.flf", "Circle")
        {
        }
    }
}

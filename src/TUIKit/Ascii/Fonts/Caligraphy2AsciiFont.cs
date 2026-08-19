namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Caligraphy2" FIGlet font (registered as "Caligraphy2"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class Caligraphy2AsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Caligraphy2AsciiFont"/> class.
        /// </summary>
        public Caligraphy2AsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Caligraphy2.flf", "Caligraphy2")
        {
        }
    }
}

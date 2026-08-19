namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Graffiti" FIGlet font (registered as "Graffiti"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class GraffitiAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GraffitiAsciiFont"/> class.
        /// </summary>
        public GraffitiAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Graffiti.flf", "Graffiti")
        {
        }
    }
}

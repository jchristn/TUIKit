namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Slant" FIGlet font (registered as "Slant"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class SlantAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SlantAsciiFont"/> class.
        /// </summary>
        public SlantAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Slant.flf", "Slant")
        {
        }
    }
}

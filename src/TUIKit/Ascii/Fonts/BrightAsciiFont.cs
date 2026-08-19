namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Bright" FIGlet font (registered as "Bright"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class BrightAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BrightAsciiFont"/> class.
        /// </summary>
        public BrightAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Bright.flf", "Bright")
        {
        }
    }
}

namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "BlurVision ASCII" FIGlet font (registered as "BlurVisionAscii"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class BlurVisionAsciiAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BlurVisionAsciiAsciiFont"/> class.
        /// </summary>
        public BlurVisionAsciiAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.BlurVisionAscii.flf", "BlurVisionAscii")
        {
        }
    }
}

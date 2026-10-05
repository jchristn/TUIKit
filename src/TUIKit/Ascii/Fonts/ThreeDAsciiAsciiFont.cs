namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "3D-ASCII" FIGlet font (registered as "ThreeDAscii"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class ThreeDAsciiAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ThreeDAsciiAsciiFont"/> class.
        /// </summary>
        public ThreeDAsciiAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.ThreeDAscii.flf", "ThreeDAscii")
        {
        }
    }
}

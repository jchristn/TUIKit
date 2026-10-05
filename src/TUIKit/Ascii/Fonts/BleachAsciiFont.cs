namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Bleach" TheDraw (converted to FIGlet) font (registered as "Bleach"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class BleachAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BleachAsciiFont"/> class.
        /// </summary>
        public BleachAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Bleach.flf", "Bleach")
        {
        }
    }
}

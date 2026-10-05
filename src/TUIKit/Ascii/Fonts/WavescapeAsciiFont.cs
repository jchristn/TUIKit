namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Wavescape" FIGlet font (registered as "Wavescape"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class WavescapeAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WavescapeAsciiFont"/> class.
        /// </summary>
        public WavescapeAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Wavescape.flf", "Wavescape")
        {
        }
    }
}

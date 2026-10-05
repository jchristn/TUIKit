namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "FateGate" TheDraw (converted to FIGlet) font (registered as "FateGate"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class FateGateAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FateGateAsciiFont"/> class.
        /// </summary>
        public FateGateAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.FateGate.flf", "FateGate")
        {
        }
    }
}

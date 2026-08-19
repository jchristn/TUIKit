namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Slant Relief" FIGlet font (registered as "SlantRelief"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class SlantReliefAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SlantReliefAsciiFont"/> class.
        /// </summary>
        public SlantReliefAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.SlantRelief.flf", "SlantRelief")
        {
        }
    }
}

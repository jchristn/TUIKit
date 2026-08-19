namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Small Slant" FIGlet font (registered as "SmallSlant"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class SmallSlantAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SmallSlantAsciiFont"/> class.
        /// </summary>
        public SmallSlantAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.SmallSlant.flf", "SmallSlant")
        {
        }
    }
}

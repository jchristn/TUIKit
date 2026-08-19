namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Small Caps" FIGlet font (registered as "SmallCaps"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class SmallCapsAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SmallCapsAsciiFont"/> class.
        /// </summary>
        public SmallCapsAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.SmallCaps.flf", "SmallCaps")
        {
        }
    }
}

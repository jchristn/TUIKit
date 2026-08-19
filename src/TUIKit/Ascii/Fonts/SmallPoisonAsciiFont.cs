namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Small Poison" FIGlet font (registered as "SmallPoison"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class SmallPoisonAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SmallPoisonAsciiFont"/> class.
        /// </summary>
        public SmallPoisonAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.SmallPoison.flf", "SmallPoison")
        {
        }
    }
}

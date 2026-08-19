namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Small Tengwar" FIGlet font (registered as "SmallTengwar"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class SmallTengwarAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SmallTengwarAsciiFont"/> class.
        /// </summary>
        public SmallTengwarAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.SmallTengwar.flf", "SmallTengwar")
        {
        }
    }
}

namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Small Isometric1" FIGlet font (registered as "SmallIsometric1"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class SmallIsometric1AsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SmallIsometric1AsciiFont"/> class.
        /// </summary>
        public SmallIsometric1AsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.SmallIsometric1.flf", "SmallIsometric1")
        {
        }
    }
}

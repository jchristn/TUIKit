namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Epic" FIGlet font (registered as "Epic"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class EpicAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EpicAsciiFont"/> class.
        /// </summary>
        public EpicAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Epic.flf", "Epic")
        {
        }
    }
}

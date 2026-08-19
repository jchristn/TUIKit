namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Chiseled" FIGlet font (registered as "Chiseled"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class ChiseledAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ChiseledAsciiFont"/> class.
        /// </summary>
        public ChiseledAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Chiseled.flf", "Chiseled")
        {
        }
    }
}

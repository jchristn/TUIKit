namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Blocks" FIGlet font (registered as "Blocks"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class BlocksAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BlocksAsciiFont"/> class.
        /// </summary>
        public BlocksAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Blocks.flf", "Blocks")
        {
        }
    }
}

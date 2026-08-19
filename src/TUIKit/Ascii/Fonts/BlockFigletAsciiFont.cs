namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Block" FIGlet font (registered as "BlockFiglet"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class BlockFigletAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BlockFigletAsciiFont"/> class.
        /// </summary>
        public BlockFigletAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.BlockFiglet.flf", "BlockFiglet")
        {
        }
    }
}

namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "miniwi" FIGlet font (registered as "Miniwi"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class MiniwiAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MiniwiAsciiFont"/> class.
        /// </summary>
        public MiniwiAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Miniwi.flf", "Miniwi")
        {
        }
    }
}

namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Ghost" FIGlet font (registered as "Ghost"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class GhostAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GhostAsciiFont"/> class.
        /// </summary>
        public GhostAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Ghost.flf", "Ghost")
        {
        }
    }
}

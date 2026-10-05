namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "The Edge" FIGlet font (registered as "TheEdge"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class TheEdgeAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TheEdgeAsciiFont"/> class.
        /// </summary>
        public TheEdgeAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.TheEdge.flf", "TheEdge")
        {
        }
    }
}

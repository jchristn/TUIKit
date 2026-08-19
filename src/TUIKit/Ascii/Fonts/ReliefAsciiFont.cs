namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Relief" FIGlet font (registered as "Relief"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class ReliefAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ReliefAsciiFont"/> class.
        /// </summary>
        public ReliefAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Relief.flf", "Relief")
        {
        }
    }
}

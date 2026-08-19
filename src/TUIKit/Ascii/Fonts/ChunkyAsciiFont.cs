namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Chunky" FIGlet font (registered as "Chunky"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class ChunkyAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ChunkyAsciiFont"/> class.
        /// </summary>
        public ChunkyAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Chunky.flf", "Chunky")
        {
        }
    }
}

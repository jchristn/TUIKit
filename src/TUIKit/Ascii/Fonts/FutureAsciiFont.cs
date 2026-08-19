namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Future" FIGlet font (registered as "Future"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class FutureAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FutureAsciiFont"/> class.
        /// </summary>
        public FutureAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Future.flf", "Future")
        {
        }
    }
}

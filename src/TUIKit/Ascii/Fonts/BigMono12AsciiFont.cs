namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Big Mono 12" FIGlet font (registered as "BigMono12"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class BigMono12AsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BigMono12AsciiFont"/> class.
        /// </summary>
        public BigMono12AsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.BigMono12.flf", "BigMono12")
        {
        }
    }
}

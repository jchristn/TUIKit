namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Big Mono 9" FIGlet font (registered as "BigMono9"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class BigMono9AsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BigMono9AsciiFont"/> class.
        /// </summary>
        public BigMono9AsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.BigMono9.flf", "BigMono9")
        {
        }
    }
}

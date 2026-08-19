namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Relief2" FIGlet font (registered as "Relief2"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class Relief2AsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Relief2AsciiFont"/> class.
        /// </summary>
        public Relief2AsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Relief2.flf", "Relief2")
        {
        }
    }
}

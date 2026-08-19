namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Big" FIGlet font (registered as "Big"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class BigAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BigAsciiFont"/> class.
        /// </summary>
        public BigAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Big.flf", "Big")
        {
        }
    }
}

namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Crawford2" FIGlet font (registered as "Crawford2"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class Crawford2AsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Crawford2AsciiFont"/> class.
        /// </summary>
        public Crawford2AsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Crawford2.flf", "Crawford2")
        {
        }
    }
}

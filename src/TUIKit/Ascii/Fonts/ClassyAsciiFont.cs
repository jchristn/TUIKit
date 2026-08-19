namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Classy" FIGlet font (registered as "Classy"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class ClassyAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ClassyAsciiFont"/> class.
        /// </summary>
        public ClassyAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Classy.flf", "Classy")
        {
        }
    }
}

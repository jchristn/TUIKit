namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Sweet" FIGlet font (registered as "Sweet"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class SweetAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SweetAsciiFont"/> class.
        /// </summary>
        public SweetAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Sweet.flf", "Sweet")
        {
        }
    }
}

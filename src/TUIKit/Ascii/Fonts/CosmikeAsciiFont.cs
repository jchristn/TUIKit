namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Cosmike" FIGlet font (registered as "Cosmike"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class CosmikeAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CosmikeAsciiFont"/> class.
        /// </summary>
        public CosmikeAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Cosmike.flf", "Cosmike")
        {
        }
    }
}

namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Alpha" FIGlet font (registered as "Alpha"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class AlphaAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AlphaAsciiFont"/> class.
        /// </summary>
        public AlphaAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Alpha.flf", "Alpha")
        {
        }
    }
}

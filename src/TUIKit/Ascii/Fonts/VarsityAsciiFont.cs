namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Varsity" FIGlet font (registered as "Varsity"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class VarsityAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="VarsityAsciiFont"/> class.
        /// </summary>
        public VarsityAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Varsity.flf", "Varsity")
        {
        }
    }
}

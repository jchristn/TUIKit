namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Crazy" FIGlet font (registered as "Crazy"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class CrazyAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CrazyAsciiFont"/> class.
        /// </summary>
        public CrazyAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Crazy.flf", "Crazy")
        {
        }
    }
}

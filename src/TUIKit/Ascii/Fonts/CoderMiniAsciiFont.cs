namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Coder Mini" FIGlet font (registered as "CoderMini"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class CoderMiniAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CoderMiniAsciiFont"/> class.
        /// </summary>
        public CoderMiniAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.CoderMini.flf", "CoderMini")
        {
        }
    }
}

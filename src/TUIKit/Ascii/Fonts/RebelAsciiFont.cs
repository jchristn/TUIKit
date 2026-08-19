namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Rebel" FIGlet font (registered as "Rebel"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class RebelAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RebelAsciiFont"/> class.
        /// </summary>
        public RebelAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Rebel.flf", "Rebel")
        {
        }
    }
}

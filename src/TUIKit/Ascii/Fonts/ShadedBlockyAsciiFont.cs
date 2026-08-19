namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Shaded Blocky" FIGlet font (registered as "ShadedBlocky"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class ShadedBlockyAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ShadedBlockyAsciiFont"/> class.
        /// </summary>
        public ShadedBlockyAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.ShadedBlocky.flf", "ShadedBlocky")
        {
        }
    }
}

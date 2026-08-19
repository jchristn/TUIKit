namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Small Mono 9" FIGlet font (registered as "SmallMono9"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class SmallMono9AsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SmallMono9AsciiFont"/> class.
        /// </summary>
        public SmallMono9AsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.SmallMono9.flf", "SmallMono9")
        {
        }
    }
}

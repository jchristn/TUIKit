namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Small Mono 12" FIGlet font (registered as "SmallMono12"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class SmallMono12AsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SmallMono12AsciiFont"/> class.
        /// </summary>
        public SmallMono12AsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.SmallMono12.flf", "SmallMono12")
        {
        }
    }
}

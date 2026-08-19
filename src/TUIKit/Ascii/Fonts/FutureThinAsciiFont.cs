namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Future Thin" FIGlet font (registered as "FutureThin"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class FutureThinAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FutureThinAsciiFont"/> class.
        /// </summary>
        public FutureThinAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.FutureThin.flf", "FutureThin")
        {
        }
    }
}

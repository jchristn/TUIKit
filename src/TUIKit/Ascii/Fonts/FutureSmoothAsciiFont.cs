namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Future Smooth" FIGlet font (registered as "FutureSmooth"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class FutureSmoothAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FutureSmoothAsciiFont"/> class.
        /// </summary>
        public FutureSmoothAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.FutureSmooth.flf", "FutureSmooth")
        {
        }
    }
}

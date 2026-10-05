namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Graceful" FIGlet font (registered as "Graceful"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class GracefulAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GracefulAsciiFont"/> class.
        /// </summary>
        public GracefulAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Graceful.flf", "Graceful")
        {
        }
    }
}

namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Bloody" FIGlet font (registered as "Bloody"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class BloodyAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BloodyAsciiFont"/> class.
        /// </summary>
        public BloodyAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Bloody.flf", "Bloody")
        {
        }
    }
}

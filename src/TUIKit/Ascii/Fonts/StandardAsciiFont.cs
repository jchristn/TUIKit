namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Standard" FIGlet font (registered as "Standard"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class StandardAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StandardAsciiFont"/> class.
        /// </summary>
        public StandardAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Standard.flf", "Standard")
        {
        }
    }
}

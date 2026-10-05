namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Twisted" FIGlet font (registered as "Twisted"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class TwistedAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TwistedAsciiFont"/> class.
        /// </summary>
        public TwistedAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Twisted.flf", "Twisted")
        {
        }
    }
}

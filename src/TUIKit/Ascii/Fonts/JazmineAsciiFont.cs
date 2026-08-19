namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Jazmine" FIGlet font (registered as "Jazmine"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class JazmineAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="JazmineAsciiFont"/> class.
        /// </summary>
        public JazmineAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Jazmine.flf", "Jazmine")
        {
        }
    }
}

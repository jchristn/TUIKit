namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "ANSI Shadow" FIGlet font (registered as "AnsiShadow"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class AnsiShadowAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AnsiShadowAsciiFont"/> class.
        /// </summary>
        public AnsiShadowAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.AnsiShadow.flf", "AnsiShadow")
        {
        }
    }
}

namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "ANSI Regular" FIGlet font (registered as "AnsiRegular"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class AnsiRegularAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AnsiRegularAsciiFont"/> class.
        /// </summary>
        public AnsiRegularAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.AnsiRegular.flf", "AnsiRegular")
        {
        }
    }
}

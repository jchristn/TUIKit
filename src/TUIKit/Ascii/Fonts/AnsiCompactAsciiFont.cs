namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "ANSI Compact" FIGlet font (registered as "AnsiCompact"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class AnsiCompactAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AnsiCompactAsciiFont"/> class.
        /// </summary>
        public AnsiCompactAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.AnsiCompact.flf", "AnsiCompact")
        {
        }
    }
}

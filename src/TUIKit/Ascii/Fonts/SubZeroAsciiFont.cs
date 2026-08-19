namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Sub-Zero" FIGlet font (registered as "SubZero"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class SubZeroAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SubZeroAsciiFont"/> class.
        /// </summary>
        public SubZeroAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.SubZero.flf", "SubZero")
        {
        }
    }
}

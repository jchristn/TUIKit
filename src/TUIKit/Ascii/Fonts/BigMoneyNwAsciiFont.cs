namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Big Money-nw" FIGlet font (registered as "BigMoneyNw"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class BigMoneyNwAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BigMoneyNwAsciiFont"/> class.
        /// </summary>
        public BigMoneyNwAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.BigMoneyNw.flf", "BigMoneyNw")
        {
        }
    }
}

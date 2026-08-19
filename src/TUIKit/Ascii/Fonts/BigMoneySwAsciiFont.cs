namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Big Money-sw" FIGlet font (registered as "BigMoneySw"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class BigMoneySwAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BigMoneySwAsciiFont"/> class.
        /// </summary>
        public BigMoneySwAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.BigMoneySw.flf", "BigMoneySw")
        {
        }
    }
}

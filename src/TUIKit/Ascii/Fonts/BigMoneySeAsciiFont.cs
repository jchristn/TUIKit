namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Big Money-se" FIGlet font (registered as "BigMoneySe"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class BigMoneySeAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BigMoneySeAsciiFont"/> class.
        /// </summary>
        public BigMoneySeAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.BigMoneySe.flf", "BigMoneySe")
        {
        }
    }
}

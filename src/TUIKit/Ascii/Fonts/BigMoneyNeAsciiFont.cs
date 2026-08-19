namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Big Money-ne" FIGlet font (registered as "BigMoneyNe"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class BigMoneyNeAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BigMoneyNeAsciiFont"/> class.
        /// </summary>
        public BigMoneyNeAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.BigMoneyNe.flf", "BigMoneyNe")
        {
        }
    }
}

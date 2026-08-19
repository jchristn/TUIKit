namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Delta Corps Priest 1" FIGlet font (registered as "DeltaCorpsPriest1"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class DeltaCorpsPriest1AsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DeltaCorpsPriest1AsciiFont"/> class.
        /// </summary>
        public DeltaCorpsPriest1AsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.DeltaCorpsPriest1.flf", "DeltaCorpsPriest1")
        {
        }
    }
}

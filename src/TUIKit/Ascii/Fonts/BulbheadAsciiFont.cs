namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Bulbhead" FIGlet font (registered as "Bulbhead"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class BulbheadAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BulbheadAsciiFont"/> class.
        /// </summary>
        public BulbheadAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Bulbhead.flf", "Bulbhead")
        {
        }
    }
}

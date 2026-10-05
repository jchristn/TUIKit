namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Lil Devil" FIGlet font (registered as "LilDevil"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class LilDevilAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LilDevilAsciiFont"/> class.
        /// </summary>
        public LilDevilAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.LilDevil.flf", "LilDevil")
        {
        }
    }
}

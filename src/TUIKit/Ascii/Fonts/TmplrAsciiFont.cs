namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Tmplr" FIGlet font (registered as "Tmplr"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class TmplrAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TmplrAsciiFont"/> class.
        /// </summary>
        public TmplrAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Tmplr.flf", "Tmplr")
        {
        }
    }
}

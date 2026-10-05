namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Emboss" FIGlet font (registered as "Emboss"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class EmbossAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EmbossAsciiFont"/> class.
        /// </summary>
        public EmbossAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Emboss.flf", "Emboss")
        {
        }
    }
}

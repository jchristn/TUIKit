namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Acrobatic" FIGlet font (registered as "Acrobatic"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class AcrobaticAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AcrobaticAsciiFont"/> class.
        /// </summary>
        public AcrobaticAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Acrobatic.flf", "Acrobatic")
        {
        }
    }
}

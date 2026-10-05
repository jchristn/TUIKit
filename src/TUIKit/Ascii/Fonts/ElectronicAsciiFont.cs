namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Electronic" FIGlet font (registered as "Electronic"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class ElectronicAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ElectronicAsciiFont"/> class.
        /// </summary>
        public ElectronicAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Electronic.flf", "Electronic")
        {
        }
    }
}

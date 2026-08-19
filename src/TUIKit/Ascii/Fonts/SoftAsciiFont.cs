namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Soft" FIGlet font (registered as "Soft"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class SoftAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SoftAsciiFont"/> class.
        /// </summary>
        public SoftAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Soft.flf", "Soft")
        {
        }
    }
}

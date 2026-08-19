namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Banner3-D" FIGlet font (registered as "Banner3D"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class Banner3DAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Banner3DAsciiFont"/> class.
        /// </summary>
        public Banner3DAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Banner3D.flf", "Banner3D")
        {
        }
    }
}

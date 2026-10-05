namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Swamp Land" FIGlet font (registered as "SwampLand"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class SwampLandAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SwampLandAsciiFont"/> class.
        /// </summary>
        public SwampLandAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.SwampLand.flf", "SwampLand")
        {
        }
    }
}

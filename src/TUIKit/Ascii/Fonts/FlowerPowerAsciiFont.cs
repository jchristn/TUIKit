namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Flower Power" FIGlet font (registered as "FlowerPower"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class FlowerPowerAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FlowerPowerAsciiFont"/> class.
        /// </summary>
        public FlowerPowerAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.FlowerPower.flf", "FlowerPower")
        {
        }
    }
}

namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Impossible" FIGlet font (registered as "Impossible"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class ImpossibleAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImpossibleAsciiFont"/> class.
        /// </summary>
        public ImpossibleAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Impossible.flf", "Impossible")
        {
        }
    }
}

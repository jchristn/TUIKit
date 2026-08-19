namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Terrace" FIGlet font (registered as "Terrace"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class TerraceAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TerraceAsciiFont"/> class.
        /// </summary>
        public TerraceAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Terrace.flf", "Terrace")
        {
        }
    }
}

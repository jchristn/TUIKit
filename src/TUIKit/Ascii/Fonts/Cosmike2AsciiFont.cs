namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Cosmike2" FIGlet font (registered as "Cosmike2"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class Cosmike2AsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Cosmike2AsciiFont"/> class.
        /// </summary>
        public Cosmike2AsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Cosmike2.flf", "Cosmike2")
        {
        }
    }
}

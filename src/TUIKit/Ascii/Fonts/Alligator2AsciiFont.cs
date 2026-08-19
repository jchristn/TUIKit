namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Alligator2" FIGlet font (registered as "Alligator2"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class Alligator2AsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Alligator2AsciiFont"/> class.
        /// </summary>
        public Alligator2AsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Alligator2.flf", "Alligator2")
        {
        }
    }
}

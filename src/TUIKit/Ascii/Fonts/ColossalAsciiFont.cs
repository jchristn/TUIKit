namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Colossal" FIGlet font (registered as "Colossal"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class ColossalAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ColossalAsciiFont"/> class.
        /// </summary>
        public ColossalAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Colossal.flf", "Colossal")
        {
        }
    }
}

namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Doom" FIGlet font (registered as "Doom"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class DoomAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DoomAsciiFont"/> class.
        /// </summary>
        public DoomAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Doom.flf", "Doom")
        {
        }
    }
}

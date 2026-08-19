namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "DOS Rebel" FIGlet font (registered as "DosRebel"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class DosRebelAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DosRebelAsciiFont"/> class.
        /// </summary>
        public DosRebelAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.DosRebel.flf", "DosRebel")
        {
        }
    }
}

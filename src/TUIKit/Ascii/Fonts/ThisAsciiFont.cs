namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "THIS" FIGlet font (registered as "This"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class ThisAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ThisAsciiFont"/> class.
        /// </summary>
        public ThisAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.This.flf", "This")
        {
        }
    }
}

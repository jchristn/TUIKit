namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Small Shadow" FIGlet font (registered as "SmallShadow"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class SmallShadowAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SmallShadowAsciiFont"/> class.
        /// </summary>
        public SmallShadowAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.SmallShadow.flf", "SmallShadow")
        {
        }
    }
}

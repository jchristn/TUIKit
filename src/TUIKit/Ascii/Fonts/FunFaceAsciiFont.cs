namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Fun Face" FIGlet font (registered as "FunFace"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class FunFaceAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FunFaceAsciiFont"/> class.
        /// </summary>
        public FunFaceAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.FunFace.flf", "FunFace")
        {
        }
    }
}

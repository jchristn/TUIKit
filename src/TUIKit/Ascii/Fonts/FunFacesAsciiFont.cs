namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Fun Faces" FIGlet font (registered as "FunFaces"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class FunFacesAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FunFacesAsciiFont"/> class.
        /// </summary>
        public FunFacesAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.FunFaces.flf", "FunFaces")
        {
        }
    }
}

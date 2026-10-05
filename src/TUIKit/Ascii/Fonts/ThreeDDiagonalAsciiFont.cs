namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "3D Diagonal" FIGlet font (registered as "ThreeDDiagonal"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class ThreeDDiagonalAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ThreeDDiagonalAsciiFont"/> class.
        /// </summary>
        public ThreeDDiagonalAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.ThreeDDiagonal.flf", "ThreeDDiagonal")
        {
        }
    }
}

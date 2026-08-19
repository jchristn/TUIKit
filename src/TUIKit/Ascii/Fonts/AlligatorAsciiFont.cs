namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Alligator" FIGlet font (registered as "Alligator"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class AlligatorAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AlligatorAsciiFont"/> class.
        /// </summary>
        public AlligatorAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Alligator.flf", "Alligator")
        {
        }
    }
}

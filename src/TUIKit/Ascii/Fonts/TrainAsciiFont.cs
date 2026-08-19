namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Train" FIGlet font (registered as "Train"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class TrainAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TrainAsciiFont"/> class.
        /// </summary>
        public TrainAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Train.flf", "Train")
        {
        }
    }
}

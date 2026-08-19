namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Small Keyboard" FIGlet font (registered as "SmallKeyboard"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class SmallKeyboardAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SmallKeyboardAsciiFont"/> class.
        /// </summary>
        public SmallKeyboardAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.SmallKeyboard.flf", "SmallKeyboard")
        {
        }
    }
}

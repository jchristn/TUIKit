namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Small Script" FIGlet font (registered as "SmallScript"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class SmallScriptAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SmallScriptAsciiFont"/> class.
        /// </summary>
        public SmallScriptAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.SmallScript.flf", "SmallScript")
        {
        }
    }
}

namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Modular" FIGlet font (registered as "Modular"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class ModularAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ModularAsciiFont"/> class.
        /// </summary>
        public ModularAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Modular.flf", "Modular")
        {
        }
    }
}

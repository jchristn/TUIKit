namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Avatar" FIGlet font (registered as "Avatar"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class AvatarAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AvatarAsciiFont"/> class.
        /// </summary>
        public AvatarAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Avatar.flf", "Avatar")
        {
        }
    }
}

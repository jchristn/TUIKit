namespace TUIKit.Ascii.Fonts
{
    using TUIKit.Ascii;

    /// <summary>
    /// The built-in "Ogre" FIGlet font (registered as "Ogre"), loaded from an embedded resource.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class OgreAsciiFont : EmbeddedFigletFont
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OgreAsciiFont"/> class.
        /// </summary>
        public OgreAsciiFont()
            : base("TUIKit.Ascii.Fonts.Data.Ogre.flf", "Ogre")
        {
        }
    }
}

namespace TUIKit.Input
{
    using System.Runtime.InteropServices;

    /// <summary>
    /// Resolves the <see cref="KeyLabelStyle"/> that matches the current operating environment so an
    /// application can present key hints using the conventions users expect on their platform. All
    /// members are thread-safe.
    /// </summary>
    public static class KeyLabel
    {
        /// <summary>
        /// Gets the label style recommended for the running operating system:
        /// <see cref="KeyLabelStyle.Symbols"/> on macOS (where <c>⌃⌥⇧⌘</c> glyphs are the norm) and
        /// <see cref="KeyLabelStyle.Ascii"/> on Windows and Linux.
        /// </summary>
        public static KeyLabelStyle Recommended
        {
            get
            {
                return RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                    ? KeyLabelStyle.Symbols
                    : KeyLabelStyle.Ascii;
            }
        }
    }
}

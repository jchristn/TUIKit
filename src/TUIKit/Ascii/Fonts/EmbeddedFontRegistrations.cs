namespace TUIKit.Ascii
{
    using System.Collections.Generic;

    /// <summary>
    /// The list of built-in embedded FIGlet fonts registered into <see cref="AsciiFontLibrary.Default"/>.
    /// The body of <see cref="All"/> is generated from the vetted, license-cleared <c>.flf</c> set (see
    /// ASCII_ART_FONTS.md). Fonts with restrictive licensing are intentionally excluded.
    /// </summary>
    internal static partial class EmbeddedFontRegistrations
    {
        internal static IEnumerable<IAsciiFont> All()
        {
            return Generated();
        }
    }
}

namespace TUIKit.Ascii
{
    using System.Collections.Generic;
    using TUIKit.Ascii.Fonts;

    /// <summary>
    /// Produces the set of fonts registered into <see cref="AsciiFontLibrary.Default"/>. Only fonts
    /// that cleared the licensing gate are listed here; restrictive-licensed fonts are intentionally
    /// absent (see ASCII_ART_FONTS.md, section 9). Each entry is a discrete font class.
    /// </summary>
    internal static class BuiltInFonts
    {
        internal static IEnumerable<IAsciiFont> All()
        {
            // The original, license-clean block font is always available.
            yield return new BlockAsciiFont();

            foreach (IAsciiFont font in EmbeddedFonts())
                yield return font;
        }

        private static IEnumerable<IAsciiFont> EmbeddedFonts()
        {
            // Populated by the generated per-font classes. Kept as its own method so the generated
            // registrations live in one place.
            return EmbeddedFontRegistrations.All();
        }
    }
}

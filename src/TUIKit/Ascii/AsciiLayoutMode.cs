namespace TUIKit.Ascii
{
    /// <summary>
    /// Controls how adjacent character glyphs are packed together when rendering ASCII art. The
    /// default for a font is carried by <see cref="AsciiFontMetrics.DefaultLayout"/>; a caller can
    /// override it through <see cref="AsciiArtOptions.Layout"/>.
    /// </summary>
    public enum AsciiLayoutMode
    {
        /// <summary>
        /// Glyphs are placed side by side with no overlap. The widest and most legible result.
        /// </summary>
        FullWidth = 0,

        /// <summary>
        /// Glyphs are moved together until their ink nearly touches, sharing only blank columns
        /// (FIGlet "kerning"/"fitting"). No characters are combined.
        /// </summary>
        Kerning = 1,

        /// <summary>
        /// Glyphs overlap by one column where the touching characters can be merged into a single
        /// glyph according to the font's <see cref="AsciiFontMetrics.SmushRules"/> (FIGlet
        /// "smushing"). The tightest result.
        /// </summary>
        Smushing = 2
    }
}

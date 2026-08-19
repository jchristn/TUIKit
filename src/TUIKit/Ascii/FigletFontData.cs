namespace TUIKit.Ascii
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The parsed result of a FIGlet font: its metrics and its character-to-glyph map. Used to hand a
    /// fully-parsed font to <see cref="AsciiFontBase"/> constructors without exposing tuples.
    /// </summary>
    internal sealed class FigletFontData
    {
        internal AsciiFontMetrics Metrics { get; }

        internal IReadOnlyDictionary<char, AsciiGlyph> Glyphs { get; }

        internal FigletFontData(AsciiFontMetrics metrics, IReadOnlyDictionary<char, AsciiGlyph> glyphs)
        {
            Metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            Glyphs = glyphs ?? throw new ArgumentNullException(nameof(glyphs));
        }
    }
}

namespace TUIKit.Ascii.Fonts
{
    using System.Collections.Generic;
    using TUIKit.Ascii;
    using TUIKit.Content;

    /// <summary>
    /// The built-in block font, drawn from TUIKit's original 5×5 <see cref="Banner"/> glyph set with
    /// ink rendered as a full block. It is original, license-clean, and needs no embedded resource, so
    /// it is the default font for <see cref="TUIKit.Widgets.AsciiArtText"/>. Covers A–Z, 0–9, space,
    /// and a few punctuation marks. Instances are immutable and thread-safe.
    /// </summary>
    public sealed class BlockAsciiFont : AsciiFontBase
    {
        private const string _Supported = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 !?.,-:";

        private BlockAsciiFont(FigletFontData data)
            : base(data.Metrics, data.Glyphs)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BlockAsciiFont"/> class.
        /// </summary>
        public BlockAsciiFont()
            : this(Build())
        {
        }

        private static FigletFontData Build()
        {
            Dictionary<char, AsciiGlyph> glyphs = new Dictionary<char, AsciiGlyph>(_Supported.Length);
            for (int i = 0; i < _Supported.Length; i++)
            {
                char c = _Supported[i];
                string[] source = BannerFont.Glyph(c);
                string[] rows = new string[source.Length];
                for (int r = 0; r < source.Length; r++)
                {
                    string line = source[r];
                    char[] cells = new char[line.Length + 1];
                    for (int col = 0; col < line.Length; col++)
                        cells[col] = line[col] == '#' ? '█' : ' ';

                    // One trailing blank column so full-width packing leaves a gap between letters.
                    cells[line.Length] = ' ';
                    rows[r] = new string(cells);
                }

                glyphs[c] = new AsciiGlyph(rows);
            }

            AsciiFontMetrics metrics = new AsciiFontMetrics(
                "Block",
                BannerFont.Rows,
                BannerFont.Rows,
                '$',
                AsciiLayoutMode.FullWidth,
                AsciiSmushRule.None);

            return new FigletFontData(metrics, glyphs);
        }
    }
}

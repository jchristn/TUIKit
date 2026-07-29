namespace TUIKit.Content
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Renders text as a large multi-row block banner (a small, self-contained FIGlet), useful for
    /// splash screens and section headers. Each character expands to a 5-row glyph from
    /// <see cref="BannerFont"/>; the rows are returned as plain strings so callers can color or place
    /// them freely. Use <see cref="Widgets.BannerText"/> to drop a banner straight into a layout.
    /// </summary>
    public static class Banner
    {
        /// <summary>
        /// Renders text into banner rows.
        /// </summary>
        /// <param name="text">The text to render. Must not be null.</param>
        /// <param name="ink">The glyph drawn for ink cells. Defaults to a full block.</param>
        /// <returns>Exactly five strings, one per banner row. Never null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public static IReadOnlyList<string> Render(string text, string ink = "█")
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            string mark = string.IsNullOrEmpty(ink) ? "█" : ink;
            StringBuilder[] rows = new StringBuilder[BannerFont.Rows];
            for (int r = 0; r < rows.Length; r++)
                rows[r] = new StringBuilder();

            for (int i = 0; i < text.Length; i++)
            {
                string[] glyph = BannerFont.Glyph(text[i]);
                for (int r = 0; r < BannerFont.Rows; r++)
                {
                    if (i > 0)
                        rows[r].Append(' ');

                    string line = glyph[r];
                    for (int c = 0; c < line.Length; c++)
                        rows[r].Append(line[c] == '#' ? mark : " ");
                }
            }

            string[] result = new string[BannerFont.Rows];
            for (int r = 0; r < rows.Length; r++)
                result[r] = rows[r].ToString();

            return result;
        }
    }
}

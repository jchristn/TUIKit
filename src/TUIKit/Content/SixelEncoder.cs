namespace TUIKit.Content
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;
    using TUIKit;

    /// <summary>
    /// Encodes a truecolor pixel image as a DECSIXEL escape sequence for terminals that support the
    /// sixel graphics protocol (xterm with sixel, mlterm, foot, WezTerm, contour, and others). Unlike
    /// <see cref="Widgets.HalfBlockImage"/>, which draws into the cell grid and works everywhere, a
    /// sixel image is a raw escape string written directly to a capable terminal at the cursor. The
    /// encoder quantizes colors to a compact palette and run-length-encodes each six-pixel band, so
    /// the output is deterministic and dependency-free. Emit the string with the terminal backend's
    /// write method after positioning the cursor.
    /// </summary>
    public static class SixelEncoder
    {
        private const int Levels = 6;

        /// <summary>
        /// Encodes a pixel grid (indexed as <c>[y, x]</c>) as a sixel escape sequence.
        /// </summary>
        /// <param name="pixels">The pixel grid. Must not be null or empty.</param>
        /// <returns>The sixel escape sequence, beginning with <c>ESC P q</c> and ending with <c>ESC \</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="pixels"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the grid has no pixels.</exception>
        public static string Encode(Color[,] pixels)
        {
            if (pixels == null)
                throw new ArgumentNullException(nameof(pixels));

            int height = pixels.GetLength(0);
            int width = pixels.GetLength(1);
            if (width == 0 || height == 0)
                throw new ArgumentException("The pixel grid must have at least one row and column.", nameof(pixels));

            List<int> paletteKeys = new List<int>();
            Dictionary<int, int> paletteIndex = new Dictionary<int, int>();
            int[,] indexed = new int[height, width];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color color = pixels[y, x];
                    int key = (Quantize(color.R) * Levels + Quantize(color.G)) * Levels + Quantize(color.B);
                    if (!paletteIndex.TryGetValue(key, out int index))
                    {
                        index = paletteKeys.Count;
                        paletteIndex[key] = index;
                        paletteKeys.Add(key);
                    }

                    indexed[y, x] = index;
                }
            }

            StringBuilder builder = new StringBuilder();
            builder.Append('\u001b').Append('P').Append('q');
            builder.Append("\"1;1;").Append(width.ToString(CultureInfo.InvariantCulture)).Append(';').Append(height.ToString(CultureInfo.InvariantCulture));

            for (int i = 0; i < paletteKeys.Count; i++)
            {
                int key = paletteKeys[i];
                int qb = key % Levels;
                int qg = (key / Levels) % Levels;
                int qr = key / (Levels * Levels);
                builder.Append('#').Append(i.ToString(CultureInfo.InvariantCulture)).Append(";2;")
                    .Append(Percent(qr)).Append(';').Append(Percent(qg)).Append(';').Append(Percent(qb));
            }

            HashSet<int> present = new HashSet<int>();
            for (int bandTop = 0; bandTop < height; bandTop += 6)
            {
                int rows = Math.Min(6, height - bandTop);
                present.Clear();
                for (int r = 0; r < rows; r++)
                {
                    for (int x = 0; x < width; x++)
                        present.Add(indexed[bandTop + r, x]);
                }

                bool firstColor = true;
                for (int index = 0; index < paletteKeys.Count; index++)
                {
                    if (!present.Contains(index))
                        continue;

                    if (!firstColor)
                        builder.Append('$');

                    firstColor = false;
                    builder.Append('#').Append(index.ToString(CultureInfo.InvariantCulture));
                    AppendBand(builder, indexed, index, bandTop, rows, width);
                }

                builder.Append('-');
            }

            builder.Append('\u001b').Append('\\');
            return builder.ToString();
        }

        private static void AppendBand(StringBuilder builder, int[,] indexed, int colorIndex, int bandTop, int rows, int width)
        {
            int x = 0;
            while (x < width)
            {
                char glyph = SixelChar(indexed, colorIndex, bandTop, rows, x);
                int run = 1;
                while (x + run < width && SixelChar(indexed, colorIndex, bandTop, rows, x + run) == glyph)
                    run++;

                if (run >= 4)
                {
                    builder.Append('!').Append(run.ToString(CultureInfo.InvariantCulture)).Append(glyph);
                }
                else
                {
                    for (int k = 0; k < run; k++)
                        builder.Append(glyph);
                }

                x += run;
            }
        }

        private static char SixelChar(int[,] indexed, int colorIndex, int bandTop, int rows, int x)
        {
            int bits = 0;
            for (int r = 0; r < rows; r++)
            {
                if (indexed[bandTop + r, x] == colorIndex)
                    bits |= 1 << r;
            }

            return (char)(0x3F + bits);
        }

        private static int Quantize(byte channel)
        {
            int level = (int)Math.Round(channel / 255.0 * (Levels - 1), MidpointRounding.AwayFromZero);
            if (level < 0)
                return 0;
            if (level > Levels - 1)
                return Levels - 1;

            return level;
        }

        private static string Percent(int level)
        {
            return (level * 100 / (Levels - 1)).ToString(CultureInfo.InvariantCulture);
        }
    }
}

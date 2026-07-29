namespace TUIKit.Content
{
    using System;
    using System.Globalization;
    using System.Text;
    using TUIKit;

    /// <summary>
    /// Encodes a truecolor pixel image using the kitty terminal graphics protocol (also supported by
    /// WezTerm, Ghostty, and Konsole). The image is transmitted as base64 24-bit RGB inside APC
    /// escape sequences, chunked to the protocol's size limit. Like <see cref="SixelEncoder"/>, the
    /// result is a raw escape string written directly to a capable terminal; for terminals without
    /// graphics support, use <see cref="Widgets.HalfBlockImage"/> instead. Output is deterministic.
    /// </summary>
    public static class KittyImageEncoder
    {
        private const int ChunkSize = 4096;

        /// <summary>
        /// Encodes a pixel grid (indexed as <c>[y, x]</c>) as one or more kitty graphics APC sequences.
        /// </summary>
        /// <param name="pixels">The pixel grid. Must not be null or empty.</param>
        /// <returns>The kitty escape sequence(s), each beginning with <c>ESC _ G</c> and ending with <c>ESC \</c>.</returns>
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

            byte[] rgb = new byte[width * height * 3];
            int p = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color color = pixels[y, x];
                    rgb[p++] = color.R;
                    rgb[p++] = color.G;
                    rgb[p++] = color.B;
                }
            }

            string payload = Convert.ToBase64String(rgb);
            StringBuilder builder = new StringBuilder();
            int offset = 0;
            bool first = true;

            while (offset < payload.Length)
            {
                int length = Math.Min(ChunkSize, payload.Length - offset);
                string chunk = payload.Substring(offset, length);
                offset += length;
                bool last = offset >= payload.Length;

                builder.Append('\u001b').Append('_').Append('G');
                if (first)
                {
                    builder.Append("a=T,f=24,s=").Append(width.ToString(CultureInfo.InvariantCulture))
                        .Append(",v=").Append(height.ToString(CultureInfo.InvariantCulture))
                        .Append(",m=").Append(last ? '0' : '1');
                }
                else
                {
                    builder.Append("m=").Append(last ? '0' : '1');
                }

                builder.Append(';').Append(chunk);
                builder.Append('\u001b').Append('\\');
                first = false;
            }

            return builder.ToString();
        }
    }
}

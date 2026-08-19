namespace TUIKit.Ascii
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Reflection;
    using System.Text;

    /// <summary>
    /// Parses FIGlet font files (<c>.flf</c>) into an <see cref="IAsciiFont"/>. Consumers use this to
    /// register their own fonts with an <see cref="AsciiFontLibrary"/> in addition to the built-in
    /// set. Only the printable ASCII range (32 to 126) is loaded; any German and code-tagged glyphs
    /// that follow are ignored. All members are thread-safe.
    /// </summary>
    public static class FigletFontLoader
    {
        private const int _FirstChar = 32;
        private const int _LastChar = 126;

        /// <summary>
        /// Loads a FIGlet font from a stream.
        /// </summary>
        /// <param name="stream">The stream containing the <c>.flf</c> content. Must not be null.</param>
        /// <param name="name">
        /// The registered name to give the font, or null to use the name from the font header.
        /// </param>
        /// <returns>The loaded font.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
        /// <exception cref="AsciiFontException">Thrown when the content is not a valid FIGlet font.</exception>
        public static IAsciiFont Load(Stream stream, string? name = null)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            string content;
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, true, 1024, true))
                content = reader.ReadToEnd();

            return new FigletFont(ParseContent(content, name));
        }

        /// <summary>
        /// Loads a FIGlet font from its text content.
        /// </summary>
        /// <param name="content">The full <c>.flf</c> file content. Must not be null.</param>
        /// <param name="name">
        /// The registered name to give the font, or null to use the name from the font header.
        /// </param>
        /// <returns>The loaded font.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="content"/> is null.</exception>
        /// <exception cref="AsciiFontException">Thrown when the content is not a valid FIGlet font.</exception>
        public static IAsciiFont Load(string content, string? name = null)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));

            return new FigletFont(ParseContent(content, name));
        }

        internal static FigletFontData LoadEmbedded(string resourceName, string registeredName)
        {
            Assembly assembly = typeof(FigletFontLoader).Assembly;
            using (Stream? stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new AsciiFontException(
                        "Embedded FIGlet font resource '" + resourceName + "' was not found in assembly '"
                        + assembly.GetName().Name + "'.");

                string content;
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, true, 1024, true))
                    content = reader.ReadToEnd();

                return ParseContent(content, registeredName);
            }
        }

        internal static FigletFontData ParseContent(string content, string? name)
        {
            // Strip a leading UTF-8 byte order mark if the reader left one in place.
            if (content.Length > 0 && content[0] == '﻿')
                content = content.Substring(1);

            string[] lines = content.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            bool hasSignature = lines.Length > 0 && lines[0].Length >= 6
                && (lines[0].StartsWith("flf2a", StringComparison.Ordinal)
                    || lines[0].StartsWith("tlf2a", StringComparison.Ordinal));
            if (!hasSignature)
                throw new AsciiFontException("Not a FIGlet font: the file must begin with the 'flf2a' or 'tlf2a' signature.");

            string header = lines[0];
            char hardBlank = header[5];

            string[] tokens = header.Substring(6).Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 5)
                throw new AsciiFontException("Malformed FIGlet header: expected at least five numeric fields.");

            int height = ParseInt(tokens[0], "height");
            int baseline = ParseInt(tokens[1], "baseline");
            int commentLines = ParseInt(tokens[4], "comment line count");
            if (height < 1)
                throw new AsciiFontException("Malformed FIGlet header: height must be one or greater.");
            if (commentLines < 0)
                throw new AsciiFontException("Malformed FIGlet header: comment line count must be zero or greater.");

            int oldLayout = ParseInt(tokens[3], "layout");
            int? fullLayout = tokens.Length >= 7 ? ParseInt(tokens[6], "full layout") : (int?)null;

            AsciiLayoutMode layout;
            AsciiSmushRule rules;
            ResolveLayout(oldLayout, fullLayout, out layout, out rules);

            int index = 1 + commentLines;
            if (index + height > lines.Length)
                throw new AsciiFontException("Truncated FIGlet font: not enough lines for the comment header.");

            char endMark = DetectEndMark(lines[index]);

            Dictionary<char, AsciiGlyph> glyphs = new Dictionary<char, AsciiGlyph>(_LastChar - _FirstChar + 1);
            for (int code = _FirstChar; code <= _LastChar; code++)
            {
                if (index + height > lines.Length)
                    throw new AsciiFontException(
                        "Truncated FIGlet font: ran out of glyph data at character code " + code + ".");

                string[] rawRows = new string[height];
                int maxWidth = 0;
                for (int r = 0; r < height; r++)
                {
                    string row = StripEndMarks(lines[index++], endMark);
                    rawRows[r] = row;
                    if (row.Length > maxWidth)
                        maxWidth = row.Length;
                }

                for (int r = 0; r < height; r++)
                {
                    if (rawRows[r].Length < maxWidth)
                        rawRows[r] = rawRows[r] + new string(' ', maxWidth - rawRows[r].Length);
                }

                glyphs[(char)code] = new AsciiGlyph(rawRows);
            }

            string fontName = string.IsNullOrEmpty(name) ? ExtractHeaderName(lines, commentLines) : name!;
            AsciiFontMetrics metrics = new AsciiFontMetrics(fontName, height, baseline, hardBlank, layout, rules);
            return new FigletFontData(metrics, glyphs);
        }

        private static void ResolveLayout(int oldLayout, int? fullLayout, out AsciiLayoutMode layout, out AsciiSmushRule rules)
        {
            if (fullLayout.HasValue)
            {
                int fl = fullLayout.Value;
                if ((fl & 128) != 0)
                {
                    layout = AsciiLayoutMode.Smushing;
                    rules = (AsciiSmushRule)(fl & 63);
                }
                else if ((fl & 64) != 0)
                {
                    layout = AsciiLayoutMode.Kerning;
                    rules = AsciiSmushRule.None;
                }
                else
                {
                    layout = AsciiLayoutMode.FullWidth;
                    rules = AsciiSmushRule.None;
                }

                return;
            }

            if (oldLayout < 0)
            {
                layout = AsciiLayoutMode.FullWidth;
                rules = AsciiSmushRule.None;
            }
            else if (oldLayout == 0)
            {
                layout = AsciiLayoutMode.Kerning;
                rules = AsciiSmushRule.None;
            }
            else
            {
                layout = AsciiLayoutMode.Smushing;
                rules = (AsciiSmushRule)(oldLayout & 63);
            }
        }

        private static char DetectEndMark(string firstGlyphLine)
        {
            if (firstGlyphLine.Length == 0)
                return '@';

            return firstGlyphLine[firstGlyphLine.Length - 1];
        }

        private static string StripEndMarks(string line, char endMark)
        {
            int end = line.Length;
            while (end > 0 && line[end - 1] == endMark)
                end--;

            return end == line.Length ? line : line.Substring(0, end);
        }

        private static string ExtractHeaderName(string[] lines, int commentLines)
        {
            // The first comment line is conventionally the font's descriptive name; fall back to the
            // first word if present, otherwise a generic label.
            if (commentLines >= 1 && lines.Length > 1)
            {
                string comment = lines[1].Trim();
                if (comment.Length > 0)
                    return comment;
            }

            return "FIGlet";
        }

        private static int ParseInt(string token, string field)
        {
            if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                throw new AsciiFontException("Malformed FIGlet header: the " + field + " field '" + token + "' is not an integer.");

            return value;
        }
    }
}

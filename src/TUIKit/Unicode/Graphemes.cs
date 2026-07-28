namespace TUIKit.Unicode
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Segments strings into extended grapheme clusters and measures their terminal width. The
    /// segmenter is a self-contained implementation of the important Unicode UAX-29 break rules so
    /// behavior is identical on every target framework. Combining marks, emoji ZWJ sequences,
    /// emoji-presentation selectors, and regional-indicator flag pairs are handled. All members are
    /// thread-safe and allocate only their result.
    /// </summary>
    public static class Graphemes
    {
        /// <summary>
        /// Splits a string into its extended grapheme clusters.
        /// </summary>
        /// <param name="text">The text to segment. Must not be null.</param>
        /// <returns>
        /// The ordered clusters. Empty when <paramref name="text"/> is empty. Never null.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public static IReadOnlyList<Grapheme> Split(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            List<Grapheme> result = new List<Grapheme>();
            if (text.Length == 0)
                return result;

            List<int> codePoints = new List<int>(text.Length);
            List<int> lengths = new List<int>(text.Length);
            DecodeCodePoints(text, codePoints, lengths);

            int index = 0;
            while (index < codePoints.Count)
            {
                int clusterStartChar = CharOffset(lengths, index);
                int clusterCharLength = lengths[index];
                int end = index + 1;

                bool sawVariationSelector16 = codePoints[index] == TextWidth.VariationSelector16;
                bool regionalPair = false;
                int baseCodePoint = codePoints[index];

                while (end < codePoints.Count && ShouldJoin(codePoints, index, end))
                {
                    if (codePoints[end] == TextWidth.VariationSelector16)
                        sawVariationSelector16 = true;

                    if (TextWidth.IsRegionalIndicator(baseCodePoint) && TextWidth.IsRegionalIndicator(codePoints[end]))
                        regionalPair = true;

                    clusterCharLength += lengths[end];
                    end++;
                }

                string clusterText = text.Substring(clusterStartChar, clusterCharLength);
                int width = ClusterWidth(baseCodePoint, sawVariationSelector16, regionalPair);
                result.Add(new Grapheme(clusterText, width));

                index = end;
            }

            return result;
        }

        /// <summary>
        /// Measures the total terminal column width of a string, honoring grapheme clustering.
        /// </summary>
        /// <param name="text">The text to measure. Must not be null.</param>
        /// <returns>The total number of columns the text occupies.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public static int MeasureWidth(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            IReadOnlyList<Grapheme> clusters = Split(text);
            int total = 0;
            for (int i = 0; i < clusters.Count; i++)
                total += clusters[i].Width;

            return total;
        }

        private static void DecodeCodePoints(string text, List<int> codePoints, List<int> lengths)
        {
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    codePoints.Add(char.ConvertToUtf32(c, text[i + 1]));
                    lengths.Add(2);
                    i += 2;
                }
                else
                {
                    codePoints.Add(c);
                    lengths.Add(1);
                    i += 1;
                }
            }
        }

        private static int CharOffset(List<int> lengths, int codePointIndex)
        {
            int offset = 0;
            for (int i = 0; i < codePointIndex; i++)
                offset += lengths[i];

            return offset;
        }

        // Decides whether the code point at 'next' extends the cluster currently ending at
        // 'next - 1' (whose base scalar is at 'clusterStart').
        private static bool ShouldJoin(List<int> codePoints, int clusterStart, int next)
        {
            int prev = codePoints[next - 1];
            int cur = codePoints[next];

            // GB3: do not break between CR and LF.
            if (prev == '\r' && cur == '\n')
                return true;

            // GB4/GB5: always break around other control characters and line breaks.
            if (IsControl(prev) || IsControl(cur))
                return false;

            // GB9: do not break before Extend or ZWJ (combining marks, variation selectors, joiner).
            if (cur == TextWidth.ZeroWidthJoiner || TextWidth.IsZeroWidth(cur))
                return true;

            // GB11: keep emoji ZWJ sequences together (pictographic ZWJ pictographic).
            if (prev == TextWidth.ZeroWidthJoiner && TextWidth.IsExtendedPictographic(cur))
                return true;

            // GB12/GB13: keep a pair of regional indicators together to form a flag.
            if (TextWidth.IsRegionalIndicator(codePoints[clusterStart])
                && TextWidth.IsRegionalIndicator(prev)
                && TextWidth.IsRegionalIndicator(cur)
                && (next - clusterStart) == 1)
            {
                return true;
            }

            return false;
        }

        private static int ClusterWidth(int baseCodePoint, bool hasVariationSelector16, bool regionalPair)
        {
            if (regionalPair)
                return 2;

            int width = TextWidth.CodePointWidth(baseCodePoint);

            if (hasVariationSelector16 && TextWidth.IsExtendedPictographic(baseCodePoint))
                return 2;

            return width;
        }

        private static bool IsControl(int codePoint)
        {
            if (codePoint == '\r' || codePoint == '\n')
                return true;

            return codePoint < 0x20 || (codePoint >= 0x7F && codePoint < 0xA0);
        }
    }
}

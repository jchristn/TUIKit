namespace TUIKit.Unicode
{
    using System;

    /// <summary>
    /// Word-boundary helpers for single-line text entry. A word is a run of graphemes that are not
    /// whitespace; there is no locale-specific word breaking. Indexes are UTF-16 indexes that always land
    /// on grapheme boundaries.
    /// </summary>
    internal static class TextWords
    {
        internal static int PreviousWordStart(string text, int index)
        {
            int i = Math.Max(0, Math.Min(index, text.Length));
            while (i > 0)
            {
                int start = TextFit.PreviousBoundary(text, i);
                if (!IsSpace(text, start, i))
                    break;
                i = start;
            }

            while (i > 0)
            {
                int start = TextFit.PreviousBoundary(text, i);
                if (IsSpace(text, start, i))
                    break;
                i = start;
            }

            return i;
        }

        internal static int NextWordStart(string text, int index)
        {
            int i = Math.Max(0, Math.Min(index, text.Length));
            while (i < text.Length)
            {
                int end = TextFit.NextBoundary(text, i);
                if (IsSpace(text, i, end))
                    break;
                i = end;
            }

            while (i < text.Length)
            {
                int end = TextFit.NextBoundary(text, i);
                if (!IsSpace(text, i, end))
                    break;
                i = end;
            }

            return i;
        }

        internal static string RemoveLastWord(string text)
        {
            return text.Substring(0, PreviousWordStart(text, text.Length));
        }

        internal static int GraphemeCount(string text)
        {
            int count = 0;
            int i = 0;
            while (i < text.Length)
            {
                i = TextFit.NextBoundary(text, i);
                count++;
            }

            return count;
        }

        internal static int IndexOfGrapheme(string text, int graphemeIndex)
        {
            int i = 0;
            int count = 0;
            while (i < text.Length && count < graphemeIndex)
            {
                i = TextFit.NextBoundary(text, i);
                count++;
            }

            return i;
        }

        private static bool IsSpace(string text, int start, int end)
        {
            for (int i = start; i < end; i++)
            {
                if (!char.IsWhiteSpace(text[i]))
                    return false;
            }

            return end > start;
        }
    }
}

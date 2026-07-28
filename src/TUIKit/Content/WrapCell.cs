namespace TUIKit.Content
{
    /// <summary>
    /// A single grapheme cluster with its width, style, and whitespace flag, used internally while
    /// wrapping styled text.
    /// </summary>
    internal readonly struct WrapCell
    {
        internal string Grapheme { get; }

        internal int Width { get; }

        internal CellStyle Style { get; }

        internal bool IsSpace { get; }

        internal WrapCell(string grapheme, int width, CellStyle style, bool isSpace)
        {
            Grapheme = grapheme;
            Width = width;
            Style = style;
            IsSpace = isSpace;
        }
    }
}

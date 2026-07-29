namespace TUIKit.Example
{
    using System;
    using TUIKit;

    /// <summary>
    /// Wraps an <see cref="ISurface"/> and forces a solid background color onto every cell that would
    /// otherwise use the terminal default. Cells that already set an explicit background (selection
    /// highlights, swatches) are passed through unchanged. The guided tour uses this to give demo
    /// panes a black background regardless of how each widget draws.
    /// </summary>
    internal sealed class SolidBackgroundSurface : ISurface
    {
        private readonly ISurface _Inner;
        private readonly Color _Background;

        internal SolidBackgroundSurface(ISurface inner, Color background)
        {
            _Inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _Background = background;
        }

        public Size Size
        {
            get { return _Inner.Size; }
        }

        public void Set(int x, int y, Cell cell)
        {
            _Inner.Set(x, y, Apply(cell));
        }

        public void Fill(Rect region, Cell cell)
        {
            _Inner.Fill(region, Apply(cell));
        }

        private Cell Apply(Cell cell)
        {
            if (!cell.Style.Background.Equals(Color.Default))
                return cell;

            CellStyle style = cell.Style.WithBackground(_Background);
            if (cell.Width <= 0)
                return Cell.Continuation(style);
            if (string.IsNullOrEmpty(cell.Grapheme) || cell.Grapheme == " ")
                return Cell.Blank(style);

            return Cell.Glyph(cell.Grapheme, style, cell.Width);
        }
    }
}

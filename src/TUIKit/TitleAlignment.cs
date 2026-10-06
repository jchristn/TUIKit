namespace TUIKit
{
    /// <summary>
    /// Where a box title sits on the top edge of a frame (see
    /// <see cref="SurfaceExtensions.DrawBox(ISurface, Rect, CellStyle, BorderStyle, string?, CellStyle, TitleAlignment, int)"/>).
    /// </summary>
    public enum TitleAlignment
    {
        /// <summary>
        /// Centered on the top edge. The default, and the only placement before 1.5.0; the title inset is
        /// ignored.
        /// </summary>
        Center = 0,

        /// <summary>
        /// Against the left corner, after the title inset. Suits sidebars, lists, and logs.
        /// </summary>
        Left = 1,

        /// <summary>
        /// Against the right corner, before the title inset.
        /// </summary>
        Right = 2
    }
}

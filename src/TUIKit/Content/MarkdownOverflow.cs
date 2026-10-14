namespace TUIKit.Content
{
    /// <summary>
    /// What the width-aware Markdown layout does with content wider than the space it has. Used for
    /// prose (<see cref="MarkdownRenderOptions.Overflow"/>), fenced code
    /// (<see cref="MarkdownRenderOptions.CodeOverflow"/>), and table cells
    /// (<see cref="MarkdownTableOptions.CellOverflow"/>).
    /// </summary>
    public enum MarkdownOverflow
    {
        /// <summary>
        /// Breaks at spaces onto further rows, hard-breaking a word that is wider than the space. Nothing is lost.
        /// </summary>
        Wrap = 0,

        /// <summary>
        /// Keeps one row, cut to fit, ending in <see cref="MarkdownRenderOptions.Ellipsis"/>.
        /// </summary>
        Truncate = 1,

        /// <summary>
        /// Keeps one row, cut to fit, with no marker.
        /// </summary>
        Clip = 2
    }
}

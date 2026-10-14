namespace TUIKit.Content
{
    /// <summary>
    /// What a Markdown table does when even its minimum column widths do not fit the layout width
    /// (see <see cref="MarkdownTableOptions.Fallback"/>).
    /// </summary>
    public enum MarkdownTableFallback
    {
        /// <summary>
        /// Renders each body row as a record of <c>Header: value</c> rows, with a rule between records. Every
        /// value stays readable at any width. This is the default.
        /// </summary>
        Stack = 0,

        /// <summary>
        /// Keeps the grid and shares the width evenly between columns, below their minimums, with a floor of one
        /// column each. Cells hard-wrap mid-word.
        /// </summary>
        Shrink = 1,

        /// <summary>
        /// Keeps the grid at the minimum column widths and cuts every row at the layout width, so the right-hand
        /// columns are lost.
        /// </summary>
        Clip = 2
    }
}

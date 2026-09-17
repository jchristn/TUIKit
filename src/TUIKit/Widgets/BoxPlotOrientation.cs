namespace TUIKit.Widgets
{
    /// <summary>
    /// The axis along which a <see cref="BoxPlotChart"/> lays out its distributions.
    /// </summary>
    public enum BoxPlotOrientation
    {
        /// <summary>
        /// Each distribution is a row: the value axis runs left to right, whiskers and the box are drawn
        /// horizontally, category labels sit in a left-hand column, and the optional scale axis is a bottom
        /// row. This is the default.
        /// </summary>
        Horizontal = 0,

        /// <summary>
        /// Each distribution is a column: the value axis runs bottom to top, whiskers and the box are drawn
        /// vertically, category labels sit on a bottom row under each column, and the optional scale axis is
        /// a left-hand gutter.
        /// </summary>
        Vertical = 1
    }
}

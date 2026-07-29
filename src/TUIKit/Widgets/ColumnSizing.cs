namespace TUIKit.Widgets
{
    /// <summary>
    /// How a <see cref="Table"/> distributes width across its columns.
    /// </summary>
    public enum ColumnSizing
    {
        /// <summary>Split the available width evenly across columns. The default (back-compatible).</summary>
        Even = 0,

        /// <summary>Size each column to fit its widest cell, clipping with an ellipsis when constrained.</summary>
        FitContent = 1
    }
}

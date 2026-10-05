namespace TUIKit.Example
{
    /// <summary>
    /// The action behind an inline button in <see cref="ClickableRowsWidget"/>: which row, and whether
    /// it opens or archives that row.
    /// </summary>
    internal sealed class RowAction
    {
        internal int Row { get; }

        internal bool Open { get; }

        internal RowAction(int row, bool open)
        {
            Row = row;
            Open = open;
        }
    }
}

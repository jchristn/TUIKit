namespace TUIKit.Widgets
{
    using System;

    /// <summary>
    /// Event data for <see cref="DataTable{T}.SortChanged"/>: the sorted column and direction. Handlers
    /// of a table with <see cref="DataTable{T}.SortLocally"/> off use it to request a sorted page from a
    /// server. Instances are immutable.
    /// </summary>
    public sealed class DataTableSortChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the zero-based sorted column index, or -1 when the sort was cleared.
        /// </summary>
        public int ColumnIndex { get; }

        /// <summary>
        /// Gets the sorted column header, or null when the sort was cleared.
        /// </summary>
        public string? ColumnName { get; }

        /// <summary>
        /// Gets a value indicating whether the sort is ascending.
        /// </summary>
        public bool Ascending { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DataTableSortChangedEventArgs"/> class.
        /// </summary>
        /// <param name="columnIndex">The column index, or -1.</param>
        /// <param name="columnName">The column header, or null.</param>
        /// <param name="ascending">Whether the sort is ascending.</param>
        public DataTableSortChangedEventArgs(int columnIndex, string? columnName, bool ascending)
        {
            ColumnIndex = columnIndex;
            ColumnName = columnName;
            Ascending = ascending;
        }
    }
}

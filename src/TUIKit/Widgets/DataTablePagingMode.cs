namespace TUIKit.Widgets
{
    /// <summary>
    /// How a <see cref="DataTable{T}"/> pages its rows. See <see cref="DataTable{T}.PagingMode"/>.
    /// </summary>
    public enum DataTablePagingMode
    {
        /// <summary>No paging: every bound row is in one scrolling list, as in 1.7. The default.</summary>
        None = 0,

        /// <summary>The table pages the rows given to <see cref="DataTable{T}.Bind"/>; indexes stay absolute.</summary>
        Local = 1,

        /// <summary>The application supplies one page at a time with <see cref="DataTable{T}.BindPage"/>; indexes are into the page.</summary>
        Remote = 2
    }
}

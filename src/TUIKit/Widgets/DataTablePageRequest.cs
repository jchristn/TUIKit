namespace TUIKit.Widgets
{
    using System;

    /// <summary>
    /// A request for one page of a server-backed <see cref="DataTable{T}"/>, raised by
    /// <see cref="DataTable{T}.PageRequested"/> and returned by <see cref="DataTable{T}.CurrentRequest"/>.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public sealed class DataTablePageRequest : EventArgs
    {
        /// <summary>
        /// Gets the requested page number, 1-based. At least 1.
        /// </summary>
        public int PageNumber { get; }

        /// <summary>
        /// Gets the page size in rows. At least 1.
        /// </summary>
        public int PageSize { get; }

        /// <summary>
        /// Gets the zero-based index of the sort column, or -1 when unsorted.
        /// </summary>
        public int SortColumnIndex { get; }

        /// <summary>
        /// Gets the sort column's <see cref="DataColumn{T}.Key"/>, or null when unsorted. This is the value a
        /// server sorts by; it does not change when the header text is translated.
        /// </summary>
        public string? SortColumnKey { get; }

        /// <summary>
        /// Gets a value indicating whether the sort is ascending. True when unsorted.
        /// </summary>
        public bool SortAscending { get; }

        /// <summary>
        /// Gets the zero-based index of the first row of the page across all pages:
        /// <c>(PageNumber - 1) * PageSize</c>, for a server that takes an offset.
        /// </summary>
        public long Offset
        {
            get { return (long)(PageNumber - 1) * PageSize; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DataTablePageRequest"/> class.
        /// </summary>
        /// <param name="pageNumber">The page number. At least 1.</param>
        /// <param name="pageSize">The page size. At least 1.</param>
        /// <param name="sortColumnIndex">The sort column index, or -1 when unsorted. At least -1.</param>
        /// <param name="sortColumnKey">The sort column key, or null when unsorted.</param>
        /// <param name="sortAscending">Whether the sort is ascending.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a number is below its minimum.</exception>
        public DataTablePageRequest(int pageNumber, int pageSize, int sortColumnIndex, string? sortColumnKey, bool sortAscending)
        {
            if (pageNumber < 1)
                throw new ArgumentOutOfRangeException(nameof(pageNumber), "Page numbers start at 1.");
            if (pageSize < 1)
                throw new ArgumentOutOfRangeException(nameof(pageSize), "The page size must be at least 1.");
            if (sortColumnIndex < -1)
                throw new ArgumentOutOfRangeException(nameof(sortColumnIndex), "The sort column index must be -1 or greater.");

            PageNumber = pageNumber;
            PageSize = pageSize;
            SortColumnIndex = sortColumnIndex;
            SortColumnKey = sortColumnKey;
            SortAscending = sortAscending;
        }
    }
}

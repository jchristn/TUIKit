namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One page of rows returned by a <see cref="DataTableLoader{T}"/> load delegate. Instances are immutable;
    /// the row list is held as given.
    /// </summary>
    /// <typeparam name="T">The row type.</typeparam>
    public sealed class DataTablePage<T>
    {
        /// <summary>
        /// Gets the rows on the page. Never null; may be empty.
        /// </summary>
        public IReadOnlyList<T> Rows { get; }

        /// <summary>
        /// Gets the total row count across all pages, or -1 when the server does not know it.
        /// </summary>
        public long TotalRows { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DataTablePage{T}"/> class.
        /// </summary>
        /// <param name="rows">The rows. Must not be null.</param>
        /// <param name="totalRows">The total row count, or -1 for unknown. At least -1.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rows"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="totalRows"/> is below -1.</exception>
        public DataTablePage(IReadOnlyList<T> rows, long totalRows)
        {
            if (totalRows < -1)
                throw new ArgumentOutOfRangeException(nameof(totalRows), "The total must be -1 (unknown) or greater.");

            Rows = rows ?? throw new ArgumentNullException(nameof(rows));
            TotalRows = totalRows;
        }
    }
}

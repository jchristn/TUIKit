namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit;

    /// <summary>
    /// A column definition for a <see cref="DataTable{T}"/>: a header, a value selector, and optional
    /// sizing, alignment, typed sorting, and per-cell styling. Columns without a fixed
    /// <see cref="Width"/> share the remaining width in proportion to <see cref="Weight"/>, never below
    /// <see cref="MinWidth"/>.
    /// </summary>
    /// <typeparam name="T">The row type.</typeparam>
    /// <remarks>Not thread-safe: configure columns before or between renders on the UI loop.</remarks>
    public sealed class DataColumn<T>
    {
        private int? _Width;
        private int _Weight = 1;
        private int _MinWidth = 1;

        /// <summary>
        /// Gets the header text. Never null.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the selector returning the cell text for a row. Never null.
        /// </summary>
        public Func<T, string> Value { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the column can be sorted.
        /// </summary>
        public bool Sortable { get; set; }

        /// <summary>
        /// Gets or sets a fixed width in columns, or null to share the remaining width by
        /// <see cref="Weight"/>. Must be at least 1 when set.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set below 1.</exception>
        public int? Width
        {
            get { return _Width; }
            set
            {
                if (value.HasValue && value.Value < 1)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Width must be at least 1.");
                _Width = value;
            }
        }

        /// <summary>
        /// Gets or sets the share of the remaining width this column receives. Defaults to 1. Must be at
        /// least 1.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set below 1.</exception>
        public int Weight
        {
            get { return _Weight; }
            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Weight must be at least 1.");
                _Weight = value;
            }
        }

        /// <summary>
        /// Gets or sets the minimum width of a weighted column. Defaults to 1. Must be at least 1.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set below 1.</exception>
        public int MinWidth
        {
            get { return _MinWidth; }
            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Minimum width must be at least 1.");
                _MinWidth = value;
            }
        }

        /// <summary>
        /// Gets or sets the cell alignment. Defaults to left.
        /// </summary>
        public CellAlignment Alignment { get; set; } = CellAlignment.Left;

        /// <summary>
        /// Gets or sets a typed comparison used for sorting. When null, <see cref="SortKey"/> is used, and
        /// when that is null too the cell text is compared ordinally (the original behavior).
        /// </summary>
        public Comparison<T>? Comparer { get; set; }

        /// <summary>
        /// Gets or sets a typed sort key (for example a number or a date) compared with
        /// <see cref="Comparer{T}.Default"/>; nulls sort first. Ignored when <see cref="Comparer"/> is set.
        /// </summary>
        public Func<T, IComparable?>? SortKey { get; set; }

        /// <summary>
        /// Gets or sets a per-cell style selector, or null. A returned style is composed over the row
        /// style (unset colors inherit the row); null leaves the cell in the row style. Not applied to the
        /// selected row while it is highlighted.
        /// </summary>
        public Func<T, CellStyle?>? CellStyle { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DataColumn{T}"/> class.
        /// </summary>
        /// <param name="name">The header. Must not be null.</param>
        /// <param name="value">The cell text selector. Must not be null.</param>
        /// <param name="sortable">Whether the column can be sorted.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public DataColumn(string name, Func<T, string> value, bool sortable = false)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Value = value ?? throw new ArgumentNullException(nameof(value));
            Sortable = sortable;
        }

        /// <summary>
        /// Compares two rows by this column's comparer, sort key, or text.
        /// </summary>
        /// <param name="x">The first row.</param>
        /// <param name="y">The second row.</param>
        /// <returns>A negative, zero, or positive number.</returns>
        public int Compare(T x, T y)
        {
            if (Comparer != null)
                return Comparer(x, y);

            if (SortKey != null)
            {
                IComparable? a = SortKey(x);
                IComparable? b = SortKey(y);
                if (a == null)
                    return b == null ? 0 : -1;
                if (b == null)
                    return 1;
                return a.CompareTo(b);
            }

            return string.Compare(Value(x), Value(y), StringComparison.Ordinal);
        }
    }
}

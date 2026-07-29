namespace TUIKit.Widgets
{
    using System;

    /// <summary>
    /// A column definition for a <see cref="DataTable{T}"/>: a header, a value selector, and whether
    /// the column can be sorted.
    /// </summary>
    /// <typeparam name="T">The row type.</typeparam>
    internal sealed class DataColumn<T>
    {
        internal string Name { get; }

        internal Func<T, string> Value { get; }

        internal bool Sortable { get; }

        internal DataColumn(string name, Func<T, string> value, bool sortable)
        {
            Name = name;
            Value = value;
            Sortable = sortable;
        }
    }
}

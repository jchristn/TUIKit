namespace TUIKit.Widgets
{
    /// <summary>
    /// The load state of a <see cref="DataTable{T}"/>. See <see cref="DataTable{T}.State"/>.
    /// </summary>
    public enum DataTableState
    {
        /// <summary>The rows are current. The default, and the state after every bind.</summary>
        Ready = 0,

        /// <summary>A load is in progress (<see cref="DataTable{T}.SetLoading"/>).</summary>
        Loading = 1,

        /// <summary>The last load failed (<see cref="DataTable{T}.SetError"/>).</summary>
        Error = 2
    }
}

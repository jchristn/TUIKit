namespace TUIKit.Widgets
{
    using System;

    /// <summary>
    /// Describes an action fired on a row of an <see cref="ActionListView{T}"/>: which row (by index and
    /// item) and which action identifier. Instances are immutable.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    public sealed class ListAction<T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ListAction{T}"/> class.
        /// </summary>
        /// <param name="index">The zero-based row index.</param>
        /// <param name="item">The row item.</param>
        /// <param name="actionId">The action identifier. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="actionId"/> is null.</exception>
        public ListAction(int index, T item, string actionId)
        {
            Index = index;
            Item = item;
            ActionId = actionId ?? throw new ArgumentNullException(nameof(actionId));
        }

        /// <summary>
        /// Gets the zero-based index of the row the action fired on.
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// Gets the item the action fired on.
        /// </summary>
        public T Item { get; }

        /// <summary>
        /// Gets the identifier of the fired action. The built-in activate action uses "activate".
        /// </summary>
        public string ActionId { get; }
    }
}

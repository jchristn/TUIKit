namespace TUIKit.Widgets
{
    using System;

    /// <summary>
    /// Event data carrying the previous and the new value of a widget property, raised by widget change
    /// events such as <see cref="TextField.ValueChanged"/> and <see cref="ListView{T}.SelectionChanged"/>.
    /// Change events fire for both user edits and programmatic sets, and only when the value actually
    /// changed. Instances are immutable and therefore thread-safe.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    public sealed class ValueChangedEventArgs<T> : EventArgs
    {
        /// <summary>
        /// Gets the value before the change.
        /// </summary>
        public T OldValue { get; }

        /// <summary>
        /// Gets the value after the change.
        /// </summary>
        public T NewValue { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ValueChangedEventArgs{T}"/> class.
        /// </summary>
        /// <param name="oldValue">The value before the change.</param>
        /// <param name="newValue">The value after the change.</param>
        public ValueChangedEventArgs(T oldValue, T newValue)
        {
            OldValue = oldValue;
            NewValue = newValue;
        }
    }
}

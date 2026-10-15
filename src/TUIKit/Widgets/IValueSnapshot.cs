namespace TUIKit.Widgets
{
    /// <summary>
    /// A widget that can report its current value as a string snapshot, so a <see cref="Form"/> with
    /// <see cref="Form.DirtyTracking"/> set to <see cref="FormDirtyTracking.Snapshot"/> can tell whether a field
    /// differs from its last saved value (and notice a revert). Implemented by <see cref="TextField"/>,
    /// <see cref="TextEditor"/>, <see cref="Checkbox"/>, <see cref="RadioGroup"/>, <see cref="Dropdown{T}"/>,
    /// <see cref="ComboBox"/>, and <see cref="CheckList{T}"/>.
    /// </summary>
    /// <remarks>Called on the UI thread; implementations need not be thread-safe.</remarks>
    public interface IValueSnapshot
    {
        /// <summary>
        /// Gets the current value as a string. Two equal snapshots mean the same value.
        /// </summary>
        /// <returns>The snapshot; null is a valid snapshot (for example "nothing selected").</returns>
        string? GetSnapshot();
    }
}

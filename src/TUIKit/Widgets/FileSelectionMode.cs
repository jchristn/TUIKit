namespace TUIKit.Widgets
{
    /// <summary>
    /// How a <see cref="FileBrowser"/> selects paths within the current directory. This is the
    /// cardinality flag for the flat, cd-style picker; the hierarchical cascading selector is a separate
    /// type (<see cref="CheckTree{T}"/> / <see cref="TUIKit.Modals.FileSelectModal"/>).
    /// </summary>
    public enum FileSelectionMode
    {
        /// <summary>
        /// No multi-select. Enter activates a single entry through the classic navigator behavior.
        /// </summary>
        None = 0,

        /// <summary>
        /// At most one checked path in the current directory; checking one clears any other.
        /// </summary>
        Single = 1,

        /// <summary>
        /// Any number of checked paths in the current directory.
        /// </summary>
        Multiple = 2
    }
}

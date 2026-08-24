namespace TUIKit.Widgets
{
    /// <summary>
    /// The tri-state check status of a node in a <see cref="CheckTree{T}"/>. A node is
    /// <see cref="Checked"/> or <see cref="Unchecked"/> when it and all of its loaded descendants share
    /// one effective state, and <see cref="Partial"/> when its loaded descendants disagree — for
    /// example a checked folder with an unchecked hole beneath it.
    /// </summary>
    public enum CheckState
    {
        /// <summary>
        /// The node is effectively unchecked, and no loaded descendant is checked.
        /// </summary>
        Unchecked = 0,

        /// <summary>
        /// The node is effectively checked, and every loaded descendant is checked.
        /// </summary>
        Checked = 1,

        /// <summary>
        /// The node's loaded descendants disagree with the node's own effective state.
        /// </summary>
        Partial = 2
    }
}

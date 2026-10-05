namespace TUIKit.Widgets
{
    /// <summary>
    /// A focusable widget that owns focusable children and moves keyboard focus among them: a hierarchical
    /// focus scope. Containers route keys to their focused child first; Tab and Shift+Tab move focus
    /// within the container and, at either end, report that focus should leave it so the parent
    /// (<see cref="FocusManager"/>, <see cref="FocusScope"/>, another container, or the host's region focus
    /// ring) moves on and enters the next container from the matching edge. Scopes nest to any depth.
    /// </summary>
    public interface IFocusContainer : IFocusable
    {
        /// <summary>
        /// Gets the focused leaf: the deepest focused descendant that is not itself a container, or null
        /// when the container has no focusable children.
        /// </summary>
        IFocusable? FocusedLeaf { get; }

        /// <summary>
        /// Moves focus to the next (or previous) focusable descendant, descending into nested containers.
        /// </summary>
        /// <param name="forward"><c>true</c> to move forward (Tab); <c>false</c> to move backward (Shift+Tab).</param>
        /// <returns>
        /// <c>true</c> when focus moved within the container; <c>false</c> when it is already at the edge in
        /// that direction, meaning the parent should move focus to the container's next sibling.
        /// </returns>
        bool MoveFocus(bool forward);

        /// <summary>
        /// Focuses the first (or last) focusable descendant, used when focus enters the container from
        /// outside: forward traversal enters at the first child, backward traversal at the last.
        /// </summary>
        /// <param name="first"><c>true</c> to focus the first descendant; <c>false</c> for the last.</param>
        void FocusEdge(bool first);
    }
}

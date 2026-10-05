namespace TUIKit.Widgets
{
    /// <summary>
    /// An optional companion to <see cref="IFocusContainer"/> that names the container's directly focused
    /// child, one level down, so the host can build the full chain from a region's widget to the focused
    /// leaf (<see cref="FocusPath"/>). <see cref="IFocusContainer.FocusedLeaf"/> jumps straight to the
    /// deepest widget and hides the containers in between; this interface exposes each step, which is
    /// what lets an application render "focus is somewhere inside this pane" and resolve key hints from
    /// the leaf outward. Every built-in container implements it. A custom container that does not still
    /// works: the path then jumps from that container directly to its focused leaf.
    /// </summary>
    /// <remarks>
    /// Implementations must be cheap and side-effect free; the host reads this after every input event
    /// and before every frame. Read it on the UI thread.
    /// </remarks>
    public interface IFocusPathNode
    {
        /// <summary>
        /// Gets the child that currently holds focus within this container, one level down, or null when
        /// focus rests on the container itself (for example a tab strip, or a collapsed section's header)
        /// or the container has no focusable children.
        /// </summary>
        IFocusable? FocusedChild { get; }
    }
}

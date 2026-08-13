namespace TUIKit.Widgets
{
    using TUIKit;

    /// <summary>
    /// Implemented by a scrollable child that can report its total content height and the bounds of its
    /// currently focused sub-region, so a hosting <see cref="ScrollView"/> can scroll the focused part
    /// into view automatically. A child that does not implement this is scrolled only by explicit keys,
    /// wheel, or <see cref="ScrollView.ScrollTo"/>.
    /// </summary>
    public interface IScrollExtent
    {
        /// <summary>
        /// Gets the full content height in cells.
        /// </summary>
        int ContentHeight { get; }

        /// <summary>
        /// Gets the bounds of the currently focused sub-region, in the child's own content coordinates.
        /// </summary>
        /// <param name="rect">When this returns true, the focused region's rectangle.</param>
        /// <returns><c>true</c> when there is a focused region; otherwise <c>false</c>.</returns>
        bool TryGetFocusRect(out Rect rect);
    }
}

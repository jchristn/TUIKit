namespace TUIKit.Widgets
{
    using TUIKit.Input;

    /// <summary>
    /// An optional companion to <see cref="IWidget"/> for widgets that respond to the mouse — wheel
    /// scrolling, click-to-activate, drag. The host performs per-frame hit-testing during its draw pass;
    /// when a mouse event falls inside a bound widget that implements this interface, the host forwards
    /// it through <see cref="HandleMouse"/> with the coordinates translated into the widget's own content
    /// rectangle (the same coordinate space its <see cref="IWidget.Render"/> draws in). Implementing this
    /// interface is optional: widgets that ignore the mouse need not implement it.
    /// </summary>
    public interface IMouseAware
    {
        /// <summary>
        /// Handles a mouse event whose coordinates are relative to this widget's content rectangle.
        /// </summary>
        /// <param name="mouse">The mouse event in widget-local coordinates. Never null.</param>
        /// <returns><c>true</c> when the event was consumed; otherwise <c>false</c>.</returns>
        bool HandleMouse(MouseEvent mouse);
    }
}

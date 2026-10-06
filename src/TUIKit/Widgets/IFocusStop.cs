namespace TUIKit.Widgets
{
    /// <summary>
    /// Optional companion to <see cref="IFocusable"/> for a widget that is enabled and can hold focus but
    /// should not be a Tab stop: toolbars, read-only previews, and status chips that respond to clicks
    /// or to programmatic focus without interrupting keyboard traversal. Marking such a widget disabled
    /// would dim it; this keeps it looking and acting enabled.
    /// </summary>
    /// <remarks>
    /// <see cref="FocusScope.IsTabStop"/> treats <see cref="IsFocusStop"/> false as "skip in Tab and
    /// Shift+Tab", while <see cref="FocusScope.IsFocusable"/> still reports the widget as able to hold
    /// focus, so <see cref="FocusScope.SetFocus(IFocusable)"/> and focus repair leave it focused.
    /// Implementations should be cheap and side-effect free; it is read on the UI thread.
    /// </remarks>
    public interface IFocusStop
    {
        /// <summary>
        /// Gets a value indicating whether Tab and Shift+Tab stop on this widget. False skips it during
        /// traversal without disabling it.
        /// </summary>
        bool IsFocusStop { get; }
    }
}

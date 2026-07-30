namespace TUIKit.Widgets
{
    /// <summary>
    /// An optional companion to <see cref="IFocusable"/> for widgets that render a visible focus state
    /// — a caret, a selection highlight, a raised border. The host and <see cref="FocusManager"/> call
    /// <see cref="OnFocusChanged"/> whenever keyboard focus enters or leaves the widget, so the next
    /// rendered frame reflects the change and focus never diverges from what is drawn. Implementing this
    /// interface is optional: a focusable widget with no distinct focused appearance need not implement
    /// it, and focus routing works either way. Implementations should be lightweight (typically flip a
    /// backing field) and must not throw.
    /// </summary>
    public interface IFocusAware
    {
        /// <summary>
        /// Called when this widget gains or loses keyboard focus.
        /// </summary>
        /// <param name="focused"><c>true</c> when the widget has just gained focus; <c>false</c> when it has just lost it.</param>
        void OnFocusChanged(bool focused);
    }
}

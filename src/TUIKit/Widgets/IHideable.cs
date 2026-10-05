namespace TUIKit.Widgets
{
    /// <summary>
    /// Lets a widget say it is currently hidden or has nothing to show, so it is not a focus stop:
    /// <see cref="FocusScope.IsFocusable"/> (and so the host's region focus ring, <see cref="FocusScope"/>,
    /// <see cref="FocusManager"/>, and every built-in container) skips a widget whose
    /// <see cref="IsVisible"/> is false, the same way it skips a disabled one. Implement it on a widget
    /// that can be hidden, or that renders nothing while it is empty or loading, so Tab never lands
    /// somewhere the user cannot see. <see cref="ButtonRow"/> implements it (hidden while it has no
    /// buttons).
    /// </summary>
    public interface IHideable
    {
        /// <summary>
        /// Gets a value indicating whether the widget is currently shown with something to interact with.
        /// </summary>
        bool IsVisible { get; }
    }
}

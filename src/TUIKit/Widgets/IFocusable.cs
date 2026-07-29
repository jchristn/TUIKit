namespace TUIKit.Widgets
{
    using TUIKit.Input;

    /// <summary>
    /// A widget that can receive keyboard focus and handle keys. Implemented by the interactive
    /// widgets so a <see cref="FocusManager"/> can route input to whichever one is focused.
    /// </summary>
    public interface IFocusable
    {
        /// <summary>
        /// Handles a key event while this widget is focused.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns><c>true</c> when the key was consumed; otherwise <c>false</c>.</returns>
        bool HandleKey(KeyEvent key);
    }
}

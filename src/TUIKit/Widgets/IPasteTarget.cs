namespace TUIKit.Widgets
{
    /// <summary>
    /// A widget that accepts a bracketed paste as one literal run of text. The host offers a paste to the
    /// focused widget when <see cref="Hosting.TuiApplication.RoutePasteToFocus"/> is on, and a
    /// <see cref="Modals.DialogModal"/> offers it to its <c>PasteTarget</c>. Containers implement it by
    /// forwarding to their focused child.
    /// </summary>
    /// <remarks>Implementations are called on the UI thread and need not be thread-safe.</remarks>
    public interface IPasteTarget
    {
        /// <summary>
        /// Handles pasted text.
        /// </summary>
        /// <param name="text">The pasted text. Must not be null; may be empty.</param>
        /// <returns><c>true</c> when the paste was consumed; otherwise <c>false</c>, so the host can offer it elsewhere.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        bool HandlePaste(string text);
    }
}

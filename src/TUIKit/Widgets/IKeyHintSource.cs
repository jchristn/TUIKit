namespace TUIKit.Widgets
{
    using System.Collections.Generic;
    using TUIKit.Input;

    /// <summary>
    /// Describes the keys a widget or container handles right now, so a status bar can list them while
    /// it holds focus. <see cref="KeyHintResolver"/> asks every node on the focus path, the focused leaf
    /// first and then each container outward, and the innermost description of a key wins. Return only
    /// keys that work in the widget's current state (for example, omit <c>Enter</c> when nothing handles
    /// activation).
    /// </summary>
    /// <remarks>
    /// Called on the UI thread, typically once per frame; keep it cheap and free of side effects.
    /// </remarks>
    public interface IKeyHintSource
    {
        /// <summary>
        /// Gets the hints for this widget's keys in its current state. Null is treated as no hints.
        /// </summary>
        /// <returns>The hints, or null.</returns>
        IReadOnlyList<KeyHint>? GetKeyHints();
    }
}

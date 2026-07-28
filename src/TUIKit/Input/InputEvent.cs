namespace TUIKit.Input
{
    using System;

    /// <summary>
    /// A single decoded input event: a key press, a bracketed paste, or a mouse event. Use
    /// <see cref="Kind"/> to determine which payload is valid. Instances are immutable.
    /// </summary>
    public sealed class InputEvent
    {
        /// <summary>
        /// Gets the kind of event.
        /// </summary>
        public InputEventKind Kind { get; }

        /// <summary>
        /// Gets the key event payload. Valid only when <see cref="Kind"/> is
        /// <see cref="InputEventKind.Key"/>.
        /// </summary>
        public KeyEvent Key { get; }

        /// <summary>
        /// Gets the pasted text. Non-null only when <see cref="Kind"/> is
        /// <see cref="InputEventKind.Paste"/>.
        /// </summary>
        public string? PasteText { get; }

        /// <summary>
        /// Gets the mouse event payload. Non-null only when <see cref="Kind"/> is
        /// <see cref="InputEventKind.Mouse"/>.
        /// </summary>
        public MouseEvent? Mouse { get; }

        private InputEvent(InputEventKind kind, KeyEvent key, string? pasteText, MouseEvent? mouse)
        {
            Kind = kind;
            Key = key;
            PasteText = pasteText;
            Mouse = mouse;
        }

        /// <summary>
        /// Creates a key input event.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns>The input event.</returns>
        public static InputEvent FromKey(KeyEvent key)
        {
            return new InputEvent(InputEventKind.Key, key, null, null);
        }

        /// <summary>
        /// Creates a paste input event.
        /// </summary>
        /// <param name="text">The pasted text. Must not be null.</param>
        /// <returns>The input event.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public static InputEvent FromPaste(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            return new InputEvent(InputEventKind.Paste, default, text, null);
        }

        /// <summary>
        /// Creates a mouse input event.
        /// </summary>
        /// <param name="mouse">The mouse event. Must not be null.</param>
        /// <returns>The input event.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mouse"/> is null.</exception>
        public static InputEvent FromMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            return new InputEvent(InputEventKind.Mouse, default, null, mouse);
        }
    }
}

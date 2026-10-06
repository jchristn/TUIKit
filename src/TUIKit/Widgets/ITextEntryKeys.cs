namespace TUIKit.Widgets
{
    using TUIKit.Input;

    /// <summary>
    /// Optional companion to <see cref="ITextEntry"/> on every target framework: tells
    /// <see cref="KeyHintResolver"/> which keys a text field really consumes, so the status bar stops
    /// advertising keys (Enter, the arrows, Backspace) that never reach the application while the field
    /// is typing, and which "leave the field" hint fits this field. <see cref="TextField"/> and
    /// <see cref="TextEditor"/> implement it; a widget implementing only <see cref="ITextEntry"/> keeps
    /// the 1.4.0 behavior. Members are read on the UI thread and should be cheap.
    /// </summary>
    public interface ITextEntryKeys
    {
        /// <summary>
        /// Returns whether the field consumes a chord while it accepts text, so a hint for it would be
        /// misleading. Printable characters are already hidden by the resolver and need not be reported.
        /// </summary>
        /// <param name="chord">The chord.</param>
        /// <returns><c>true</c> when the field consumes the chord.</returns>
        bool ConsumesChord(KeyChord chord);

        /// <summary>
        /// Gets the hint shown while typing for leaving this field (for example <c>Esc</c> Back to list,
        /// or <c>Tab</c> Next field), or null to use <see cref="KeyHintResolver.LeaveTextHint"/>.
        /// </summary>
        KeyHint? LeaveHint { get; }
    }
}

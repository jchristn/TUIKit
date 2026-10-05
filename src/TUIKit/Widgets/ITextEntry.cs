namespace TUIKit.Widgets
{
    /// <summary>
    /// Marks a widget that takes typed text: while it holds focus and <see cref="AcceptsText"/> is true,
    /// printable keys go into it rather than triggering shortcuts. <see cref="Input.KeyHintResolver"/>
    /// uses this to stop advertising single-letter shortcuts that would, at that moment, type a letter,
    /// and to lead with how to leave the field. <see cref="TextField"/>, <see cref="TextEditor"/>, and
    /// <see cref="ComboBox"/> implement it; implement it on any custom widget that captures printable keys.
    /// </summary>
    public interface ITextEntry
    {
        /// <summary>
        /// Gets a value indicating whether printable keys are currently inserted as text. False while the
        /// widget is read-only or disabled, so normal shortcuts apply again.
        /// </summary>
        bool AcceptsText { get; }
    }
}

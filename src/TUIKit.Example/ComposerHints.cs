namespace TUIKit.Example
{
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Widgets;

    // Wraps the composer so it contributes chat-style hints: "/" stays while typing, and help reads
    // F1 instead of "?" while the composer has the keyboard.
    internal sealed class ComposerHints : IWidget, IFocusable, IFocusAware, IFocusPathNode, IKeyHintSource, IMouseAware
    {
        private readonly TextEditor _Editor;

        internal ComposerHints(TextEditor editor)
        {
            _Editor = editor;
        }

        public IFocusable? FocusedChild
        {
            get { return _Editor; }
        }

        public IReadOnlyList<KeyHint>? GetKeyHints()
        {
            return new[]
            {
                new KeyHint("/", "Command").WhileTyping(),
                new KeyHint("?", "Help").WithTypingAlternative(KeyHint.For("f1", "Help")),
                KeyHint.For("enter", "Send")
            };
        }

        public bool HandleKey(KeyEvent key)
        {
            return _Editor.HandleKey(key);
        }

        public void OnFocusChanged(bool focused)
        {
            _Editor.OnFocusChanged(focused);
        }

        public bool HandleMouse(MouseEvent mouse)
        {
            return _Editor.HandleMouse(mouse);
        }

        public Size Measure(Size available)
        {
            return available;
        }

        public void Render(ISurface surface)
        {
            _Editor.Render(surface);
        }
    }
}

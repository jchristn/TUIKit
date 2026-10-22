namespace TUIKit.Example
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Hosting;
    using TUIKit.Input;
    using TUIKit.Theming;
    using TUIKit.Unicode;
    using TUIKit.Widgets;

    internal sealed class ConfirmPromptDemoWidget : IWidget, IFocusable, IMouseAware, IKeyHintSource
    {
        private readonly TuiApplication _App;
        private readonly ConfirmPrompt _Prompt = new ConfirmPrompt("Proceed with installation?", true);
        private Theme? _AppliedTheme;

        internal ConfirmPromptDemoWidget(TuiApplication app)
        {
            _App = app;
        }

        /// <inheritdoc/>
        public bool HandleKey(KeyEvent key)
        {
            if (key.Code == KeyCode.Character && key.Modifiers == KeyModifiers.None)
            {
                if (key.Rune == 'r')
                {
                    _Prompt.Reset(true);
                    return true;
                }
                if (key.Rune == 'g')
                {
                    _Prompt.UseAsciiGlyphs = !_Prompt.UseAsciiGlyphs;
                    return true;
                }
            }
            return _Prompt.HandleKey(key);
        }

        /// <inheritdoc/>
        public bool HandleMouse(MouseEvent mouse)
        {
            return _Prompt.HandleMouse(mouse);
        }

        /// <inheritdoc/>
        public IReadOnlyList<KeyHint>? GetKeyHints()
        {
            List<KeyHint> hints = new List<KeyHint>(_Prompt.GetKeyHints() ?? Array.Empty<KeyHint>());
            hints.Add(new KeyHint("R", "Reset"));
            hints.Add(new KeyHint("G", "Glyphs"));
            return hints;
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            return available;
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (!ReferenceEquals(_AppliedTheme, _App.Theme))
            {
                _AppliedTheme = _App.Theme;
                _Prompt.ApplyTheme(_App.Theme);
            }
            int height = _Prompt.Measure(surface.Size).Height;
            _Prompt.Render(new SurfaceView(surface, new Rect(0, 0, surface.Size.Width, height)));
            string status = !_Prompt.IsCompleted ? "Left/Right or click to select; Enter submits; Esc cancels."
                : _Prompt.Result.HasValue ? "Submitted: " + (_Prompt.Result.Value ? "Yes" : "No") + ". Press R to try again."
                : "Cancelled (no answer submitted). Press R to try again.";
            surface.DrawText(0, height + 1, TextFit.Ellipsize(status, surface.Size.Width), _App.Theme.Text);
            surface.DrawText(0, height + 3, TextFit.Ellipsize("G: Unicode/ASCII   R: reset   Ctrl+T: theme", surface.Size.Width), _App.Theme.Disabled);
        }
    }
}

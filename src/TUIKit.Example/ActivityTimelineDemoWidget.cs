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

    internal sealed class ActivityTimelineDemoWidget : IWidget, IFocusable, IMouseAware, IKeyHintSource
    {
        private readonly TuiApplication _App;
        private readonly ActivityTimeline _Timeline = new ActivityTimeline();
        private ActivityEntry _Install;
        private readonly ScrollView _Scroll;
        private bool _FollowTail;
        private Theme? _AppliedTheme;

        internal ActivityTimelineDemoWidget(TuiApplication app)
        {
            _App = app;
            _Timeline.Add("Source: https://github.com/blader/humanizer.git");
            _Timeline.Add("Repository cloned");
            _Timeline.Add("Found 1 skill");
            _Install = _Timeline.Add("Skill: humanizer", ActivityStatus.Active);
            _Scroll = new ScrollView(_Timeline, 1, 1) { ShowHorizontalScrollbar = false };
        }

        /// <inheritdoc/>
        public bool HandleKey(KeyEvent key)
        {
            if (key.Modifiers != KeyModifiers.None)
                return false;
            if (key.Code == KeyCode.Enter)
            {
                _Install.Status = ActivityStatus.Completed;
                _Install.Detail = "Installed successfully.";
                _FollowTail = true;
                return true;
            }
            if (key.Code != KeyCode.Character)
                return _Scroll.HandleKey(key);
            switch (key.Rune)
            {
                case 'g': _Timeline.UseAsciiGlyphs = !_Timeline.UseAsciiGlyphs; return true;
                case 's': _Timeline.Spacing = _Timeline.Spacing == 0 ? 1 : 0; return true;
                case 'f':
                    _Install.Status = ActivityStatus.Failed;
                    _Install.Detail = "Permission denied. Choose another directory and retry.";
                    _FollowTail = true;
                    return true;
                case 'r':
                    _Install.Status = ActivityStatus.Pending;
                    _Install.Detail = null;
                    return true;
                case 'a':
                    _Install = _Timeline.Add("Another installation target", ActivityStatus.Active, "Long activity details wrap while preserving the connector gutter.");
                    _FollowTail = true;
                    return true;
                default: return _Scroll.HandleKey(key);
            }
        }

        /// <inheritdoc/>
        public IReadOnlyList<KeyHint>? GetKeyHints()
        {
            return new[] { new KeyHint("Enter", "Complete"), new KeyHint("F", "Fail"), new KeyHint("R", "Reset"),
                new KeyHint("↑/↓", "Scroll"), new KeyHint("A", "Append"), new KeyHint("G", "Glyphs"), new KeyHint("S", "Spacing") };
        }

        /// <inheritdoc/>
        public bool HandleMouse(MouseEvent mouse)
        {
            return mouse.Y >= 3 && _Scroll.HandleMouse(new MouseEvent(mouse.Kind, mouse.Button, mouse.X, mouse.Y - 3, mouse.Modifiers, mouse.ClickCount));
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
                _Timeline.ApplyTheme(_App.Theme);
            }
            surface.DrawText(0, 0, TextFit.Ellipsize("Enter: complete  F: fail  R: reset  A: append", surface.Size.Width), _App.Theme.Text);
            surface.DrawText(0, 1, TextFit.Ellipsize("G: Unicode/ASCII  S: compact/spaced  Ctrl+T: theme", surface.Size.Width), _App.Theme.Disabled);
            if (surface.Size.Height > 3 && surface.Size.Width > 0)
            {
                int width = Math.Max(1, surface.Size.Width - 1);
                int height = Math.Max(1, _Timeline.Measure(new Size(width, int.MaxValue)).Height);
                _Scroll.SetContentSize(width, height);
                if (_FollowTail)
                {
                    _Scroll.ScrollTo(0, height);
                    _FollowTail = false;
                }
                _Scroll.Render(new SurfaceView(surface, new Rect(0, 3, surface.Size.Width, surface.Size.Height - 3)));
            }
        }
    }
}

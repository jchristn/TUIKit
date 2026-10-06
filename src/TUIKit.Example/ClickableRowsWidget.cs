namespace TUIKit.Example
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Hosting;
    using TUIKit.Input;
    using TUIKit.Modals;
    using TUIKit.Widgets;

    /// <summary>
    /// A custom-rendered list whose rows carry inline buttons, <c>[Open] o</c> and <c>[Archive] x</c>.
    /// Clicks are resolved by a <see cref="ClickRegionMap{TAction}"/> recorded during render, and every
    /// button has a key, so the list works the same from the keyboard: Up/Down pick a row, <c>o</c> and
    /// <c>x</c> act on it. Each action raises a toast; doing the same thing twice shows the notification
    /// center coalescing the repeat into one toast with a count.
    /// </summary>
    internal sealed class ClickableRowsWidget : IWidget, IFocusable, IFocusAware, IMouseAware, IKeyHintSource
    {
        private readonly TuiApplication _App;
        private readonly List<string> _Items;
        private readonly ClickRegionMap<RowAction> _Map = new ClickRegionMap<RowAction>();
        private int _Selected;
        private int _Top;
        private bool _Focused = true;

        internal ClickableRowsWidget(TuiApplication app, IEnumerable<string> items)
        {
            _App = app ?? throw new ArgumentNullException(nameof(app));
            _Items = new List<string>(items ?? throw new ArgumentNullException(nameof(items)));
            _Map.Invoked += region => Act(region.Action);
        }

        public bool HandleKey(KeyEvent key)
        {
            if (_Items.Count == 0)
                return false;

            if (key.Code == KeyCode.Up)
            {
                _Selected = Math.Max(0, _Selected - 1);
                return true;
            }

            if (key.Code == KeyCode.Down)
            {
                _Selected = Math.Min(_Items.Count - 1, _Selected + 1);
                return true;
            }

            if (key.Code == KeyCode.Character && key.Modifiers == KeyModifiers.None && (key.Rune == 'o' || key.Rune == 'x'))
            {
                Act(new RowAction(_Selected, key.Rune == 'o'));
                return true;
            }

            return false;
        }

        public IReadOnlyList<KeyHint>? GetKeyHints()
        {
            return new[]
            {
                new KeyHint("O", "Open", 5),
                new KeyHint("X", "Archive", 4),
                new KeyHint("Up/Down", "Pick row")
            };
        }

        public void OnFocusChanged(bool focused)
        {
            _Focused = focused;
        }

        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            if (_Map.HandleMouse(mouse))
                return true;

            if (mouse.Kind == MouseEventKind.Press && mouse.Button == MouseButton.Left && _Top + mouse.Y < _Items.Count)
            {
                _Selected = _Top + mouse.Y;
                return true;
            }

            return false;
        }

        public Size Measure(Size available)
        {
            return available;
        }

        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            _Map.Clear();
            int height = surface.Size.Height;
            int width = surface.Size.Width;
            if (height <= 0 || width <= 0)
                return;

            if (_Selected < _Top)
                _Top = _Selected;
            else if (_Selected >= _Top + height)
                _Top = _Selected - height + 1;

            InlineButtonStyle buttons = InlineButtonStyle.FromTheme(_App.Theme);
            int nameWidth = 0;
            foreach (string item in _Items)
                nameWidth = Math.Max(nameWidth, item.Length);

            for (int row = 0; row < height && _Top + row < _Items.Count; row++)
            {
                int index = _Top + row;
                bool selected = index == _Selected;
                CellStyle text = selected && _Focused ? _App.Theme.Selection : _App.Theme.Text;
                surface.Fill(new Rect(0, row, width, 1), Cell.Blank(selected && _Focused ? text : CellStyle.Default));
                int x = surface.DrawText(0, row, (selected ? "> " : "  ") + _Items[index].PadRight(nameWidth), text);
                x += 1;
                x += InlineButton.Draw(surface, x, row, "Open", "o", new RowAction(index, true), _Map, buttons);
                x += 1;
                InlineButton.Draw(surface, x, row, "Archive", "x", new RowAction(index, false), _Map, buttons);
            }
        }

        private void Act(RowAction action)
        {
            _Selected = action.Row;
            string verb = action.Open ? "Opened " : "Archived ";
            string item = _Items[action.Row];

            // A fresh lambda on every call: 1.5.0 coalesces repeats by the action's key, and the newest
            // callback wins. Ctrl+O runs it (TuiApplication.InvokeLatestNotificationAction).
            NotificationOptions options = new NotificationOptions
            {
                TimeoutMilliseconds = 3000,
                Actions = new[] { new NotificationAction("Show", () => _Selected = _Items.IndexOf(item), item) }
            };
            _App.Notify(verb + item, action.Open ? NotificationSeverity.Info : NotificationSeverity.Success, options);
        }
    }
}

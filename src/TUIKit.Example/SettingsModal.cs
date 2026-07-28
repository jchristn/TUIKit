namespace TUIKit.Example
{
    using System;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Layout;
    using TUIKit.Modals;
    using TUIKit.Widgets;

    /// <summary>
    /// A settings form modal that demonstrates the input-widget kit with tab order: a radio group for
    /// the theme, a checkbox for ASCII borders, and a text field. Tab moves focus between fields, Enter
    /// applies, Escape cancels. The result is a <see cref="SettingsResult"/> or null when cancelled.
    /// </summary>
    internal sealed class SettingsModal : Modal
    {
        private readonly RadioGroup _Theme = new RadioGroup(new[] { "Dark", "Light", "HighContrast" });
        private readonly Checkbox _Ascii = new Checkbox("ASCII borders");
        private readonly TextField _Label = new TextField();
        private int _Focus;

        internal SettingsModal(string initialLabel)
        {
            _Label.Value = initialLabel ?? string.Empty;
            _Theme.HandleKey(default);
        }

        public override bool HandleKey(KeyEvent key)
        {
            if (key.Code == KeyCode.Escape)
            {
                RequestClose(null);
                return true;
            }

            if (key.Code == KeyCode.Enter)
            {
                Close(new SettingsResult(_Theme.SelectedOption, _Ascii.Checked, _Label.Value));
                return true;
            }

            if (key.Code == KeyCode.Tab)
            {
                _Focus = (_Focus + 1) % 3;
                _Label.IsFocused = _Focus == 2;
                return true;
            }

            switch (_Focus)
            {
                case 0:
                    _Theme.HandleKey(key);
                    break;
                case 1:
                    _Ascii.HandleKey(key);
                    break;
                default:
                    _Label.HandleKey(key);
                    break;
            }

            return true;
        }

        public override void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            Padding pad = ContentPadding;
            const int contentRows = 7; // theme label, 3 radio rows, checkbox, label, field
            int contentWidth = Math.Min(40, surface.Size.Width - 4 - pad.Horizontal);
            if (contentWidth < 12)
                return;

            int width = contentWidth + 2 + pad.Horizontal;
            int height = contentRows + 2 + pad.Vertical;
            if (height > surface.Size.Height || width > surface.Size.Width)
                return;

            int x = (surface.Size.Width - width) / 2;
            int y = (surface.Size.Height - height) / 2;
            Rect box = new Rect(x, y, width, height);

            surface.Fill(box, Cell.Blank(CellStyle.Default));
            surface.DrawBox(box, CellStyle.Default.WithForeground(Color.FromPalette(6)), "Settings (Tab / Enter)");

            if (surface is BufferSurface buffer)
            {
                int cx = x + 1 + pad.Left;
                int cy = y + 1 + pad.Top;
                CellStyle labelStyle = CellStyle.Default.WithForeground(Color.FromPalette(3));

                surface.DrawText(cx, cy, "Theme:" + (_Focus == 0 ? "  <" : ""), labelStyle);
                _Theme.Render(buffer.CreateView(new Rect(cx, cy + 1, contentWidth, 3)));

                surface.DrawText(cx, cy + 4, _Focus == 1 ? "> " : "  ", CellStyle.Default);
                _Ascii.Render(buffer.CreateView(new Rect(cx + 2, cy + 4, contentWidth - 2, 1)));

                surface.DrawText(cx, cy + 5, "Label:" + (_Focus == 2 ? "  <" : ""), labelStyle);
                _Label.Render(buffer.CreateView(new Rect(cx, cy + 6, contentWidth, 1)));
            }
        }
    }
}

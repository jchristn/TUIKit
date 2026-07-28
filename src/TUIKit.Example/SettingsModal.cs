namespace TUIKit.Example
{
    using System;
    using TUIKit;
    using TUIKit.Input;
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

            int width = Math.Min(44, surface.Size.Width - 4);
            int height = Math.Min(11, surface.Size.Height - 2);
            if (width < 10 || height < 8)
                return;

            int x = (surface.Size.Width - width) / 2;
            int y = (surface.Size.Height - height) / 2;
            Rect box = new Rect(x, y, width, height);

            surface.Fill(box, Cell.Blank(CellStyle.Default));
            surface.DrawBox(box, CellStyle.Default.WithForeground(Color.FromPalette(6)), "Settings  (Tab to move, Enter to apply)");

            if (surface is BufferSurface buffer)
            {
                surface.DrawText(x + 2, y + 1, "Theme:" + (_Focus == 0 ? "  <" : ""), CellStyle.Default.WithForeground(Color.FromPalette(3)));
                _Theme.Render(buffer.CreateView(new Rect(x + 2, y + 2, width - 4, 3)));

                surface.DrawText(x + 2, y + 5, _Focus == 1 ? "> " : "  ", CellStyle.Default);
                _Ascii.Render(buffer.CreateView(new Rect(x + 4, y + 5, width - 6, 1)));

                surface.DrawText(x + 2, y + 7, "Label:" + (_Focus == 2 ? "  <" : ""), CellStyle.Default.WithForeground(Color.FromPalette(3)));
                _Label.Render(buffer.CreateView(new Rect(x + 2, y + 8, width - 4, 1)));
            }
        }
    }
}

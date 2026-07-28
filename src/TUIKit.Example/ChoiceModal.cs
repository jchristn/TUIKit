namespace TUIKit.Example
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Layout;
    using TUIKit.Modals;
    using TUIKit.Widgets;

    /// <summary>
    /// A modal that presents a scrollable list of choices (used as the command palette). The result is
    /// the zero-based index chosen, or -1 when cancelled. Demonstrates hosting a list widget inside a
    /// modal.
    /// </summary>
    internal sealed class ChoiceModal : Modal
    {
        private readonly string _Title;
        private readonly ListView _List = new ListView();

        internal ChoiceModal(string title, IReadOnlyList<string> options)
        {
            _Title = title ?? throw new ArgumentNullException(nameof(title));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            _List.SetItems(options);
        }

        public override bool HandleKey(KeyEvent key)
        {
            if (key.Code == KeyCode.Enter)
            {
                Close(_List.SelectedIndex);
                return true;
            }

            if (key.Code == KeyCode.Escape)
            {
                RequestClose(-1);
                return true;
            }

            _List.HandleKey(key);
            return true;
        }

        public override void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            Padding pad = ContentPadding;
            int innerWidth = Math.Min(46, surface.Size.Width - 4 - pad.Horizontal);
            int innerHeight = Math.Min(_List.Items.Count, surface.Size.Height - 4 - pad.Vertical);
            if (innerWidth < 4 || innerHeight < 1)
                return;

            int width = innerWidth + 2 + pad.Horizontal;
            int height = innerHeight + 2 + pad.Vertical;
            int x = (surface.Size.Width - width) / 2;
            int y = (surface.Size.Height - height) / 2;
            Rect box = new Rect(x, y, width, height);

            surface.Fill(box, Cell.Blank(CellStyle.Default));
            surface.DrawBox(box, CellStyle.Default.WithForeground(Color.FromPalette(6)), _Title);

            if (surface is BufferSurface buffer)
            {
                BufferSurface inner = buffer.CreateView(new Rect(x + 1 + pad.Left, y + 1 + pad.Top, innerWidth, innerHeight));
                _List.Render(inner);
            }
        }
    }
}

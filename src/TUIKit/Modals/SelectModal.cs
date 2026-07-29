namespace TUIKit.Modals
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Layout;
    using TUIKit.Widgets;

    /// <summary>
    /// A modal that presents a list of options. Up/Down move, Enter chooses (returning the zero-based
    /// index), and Escape cancels (returning -1).
    /// </summary>
    public sealed class SelectModal : Modal
    {
        private readonly string _Title;
        private readonly ListView _List = new ListView();

        /// <summary>
        /// Initializes a new instance of the <see cref="SelectModal"/> class.
        /// </summary>
        /// <param name="title">The title. Must not be null.</param>
        /// <param name="options">The options. Must not be null or empty.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="options"/> is empty.</exception>
        public SelectModal(string title, IReadOnlyList<string> options)
        {
            _Title = title ?? throw new ArgumentNullException(nameof(title));
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (options.Count == 0)
                throw new ArgumentException("At least one option is required.", nameof(options));

            List<string> copy = new List<string>(options.Count);
            for (int i = 0; i < options.Count; i++)
                copy.Add(options[i]);

            _List.SetItems(copy);
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public override void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            Padding pad = ContentPadding;
            int innerWidth = Math.Min(46, surface.Size.Width - 2 - pad.Horizontal);
            int innerHeight = Math.Min(_List.Items.Count, surface.Size.Height - 2 - pad.Vertical);
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
                _List.Render(buffer.CreateView(new Rect(x + 1 + pad.Left, y + 1 + pad.Top, innerWidth, innerHeight)));
        }
    }
}

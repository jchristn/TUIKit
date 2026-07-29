namespace TUIKit.Modals
{
    using System;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Layout;
    using TUIKit.Widgets;

    /// <summary>
    /// A modal that asks for a line of text. Enter submits the value; Escape cancels (returning null).
    /// </summary>
    public sealed class PromptModal : Modal
    {
        private readonly string _Title;
        private readonly TextField _Field = new TextField { IsFocused = true };

        /// <summary>
        /// Initializes a new instance of the <see cref="PromptModal"/> class.
        /// </summary>
        /// <param name="title">The prompt title. Must not be null.</param>
        /// <param name="initial">The initial value. Must not be null. Defaults to empty.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public PromptModal(string title, string initial = "")
        {
            _Title = title ?? throw new ArgumentNullException(nameof(title));
            _Field.Value = initial ?? throw new ArgumentNullException(nameof(initial));
        }

        /// <summary>
        /// Gets the current text value.
        /// </summary>
        public string Value
        {
            get { return _Field.Value; }
        }

        /// <inheritdoc/>
        public override bool HandleKey(KeyEvent key)
        {
            if (key.Code == KeyCode.Enter)
            {
                Close(_Field.Value);
                return true;
            }

            if (key.Code == KeyCode.Escape)
            {
                RequestClose(null);
                return true;
            }

            _Field.HandleKey(key);
            return true;
        }

        /// <inheritdoc/>
        public override void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            Padding pad = ContentPadding;
            int contentWidth = Math.Min(48, surface.Size.Width - 2 - pad.Horizontal);
            if (contentWidth < 8)
                return;

            int width = contentWidth + 2 + pad.Horizontal;
            int height = 1 + 2 + pad.Vertical;
            int x = (surface.Size.Width - width) / 2;
            int y = (surface.Size.Height - height) / 2;
            Rect box = new Rect(x, y, width, height);

            surface.Fill(box, Cell.Blank(CellStyle.Default));
            surface.DrawBox(box, CellStyle.Default.WithForeground(Color.FromPalette(6)), _Title);

            if (surface is BufferSurface buffer)
                _Field.Render(buffer.CreateView(new Rect(x + 1 + pad.Left, y + 1 + pad.Top, contentWidth, 1)));
        }
    }
}

namespace TUIKit.Modals
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Input;

    /// <summary>
    /// A simple modal showing a title, a message, and a row of buttons. Left/Right or Tab move the
    /// selection, Enter chooses, and Escape cancels. The result is the zero-based index of the chosen
    /// button, or -1 when cancelled. Serves as the confirmation dialog in the example harness.
    /// </summary>
    public sealed class MessageModal : Modal
    {
        private readonly string _Title;
        private readonly string _Message;
        private readonly string[] _Buttons;
        private readonly CellStyle _BorderStyle;
        private readonly CellStyle _SelectedStyle;
        private int _Selected;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageModal"/> class.
        /// </summary>
        /// <param name="title">The dialog title. Must not be null.</param>
        /// <param name="message">The message body. Must not be null.</param>
        /// <param name="buttons">The button labels. Must not be null or empty.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="buttons"/> is empty.</exception>
        public MessageModal(string title, string message, IReadOnlyList<string> buttons)
        {
            if (title == null)
                throw new ArgumentNullException(nameof(title));
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            if (buttons == null)
                throw new ArgumentNullException(nameof(buttons));
            if (buttons.Count == 0)
                throw new ArgumentException("At least one button is required.", nameof(buttons));

            _Title = title;
            _Message = message;
            _Buttons = new string[buttons.Count];
            for (int i = 0; i < buttons.Count; i++)
                _Buttons[i] = buttons[i];

            _BorderStyle = CellStyle.Default.WithForeground(Color.FromPalette(6));
            _SelectedStyle = CellStyle.Default
                .WithForeground(Color.FromRgb(0, 0, 0))
                .WithBackground(Color.FromPalette(6));
            _Selected = 0;
        }

        /// <summary>
        /// Gets the zero-based index of the currently highlighted button.
        /// </summary>
        public int SelectedIndex
        {
            get { return _Selected; }
        }

        /// <inheritdoc/>
        public override bool HandleKey(KeyEvent key)
        {
            switch (key.Code)
            {
                case KeyCode.Left:
                    _Selected = _Selected > 0 ? _Selected - 1 : _Buttons.Length - 1;
                    return true;
                case KeyCode.Right:
                case KeyCode.Tab:
                    _Selected = (_Selected + 1) % _Buttons.Length;
                    return true;
                case KeyCode.Enter:
                    Close(_Selected);
                    return true;
                case KeyCode.Escape:
                    RequestClose(-1);
                    return true;
                default:
                    return true; // Trap all input while modal.
            }
        }

        /// <inheritdoc/>
        public override void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            int screenWidth = surface.Size.Width;
            int screenHeight = surface.Size.Height;
            TUIKit.Layout.Padding pad = ContentPadding;

            // Content is inset from the border by ContentPadding on every side.
            int frame = 2 + pad.Horizontal;
            int contentWidth = Math.Min(screenWidth - frame, Math.Max(28, TUIKit.Unicode.Graphemes.MeasureWidth(_Message)));
            if (contentWidth < 8)
                contentWidth = Math.Max(8, screenWidth - frame);

            IReadOnlyList<StyledText> messageLines = TextWrapper.Wrap(Text.From(_Message), contentWidth);
            int contentHeight = messageLines.Count + 1 + 1; // message lines, a gap, then the buttons row
            int boxWidth = contentWidth + 2 + pad.Horizontal;
            int boxHeight = contentHeight + 2 + pad.Vertical;

            int boxX = Math.Max(0, (screenWidth - boxWidth) / 2);
            int boxY = Math.Max(0, (screenHeight - boxHeight) / 2);
            Rect box = new Rect(boxX, boxY, boxWidth, boxHeight);

            surface.Fill(box, Cell.Blank(CellStyle.Default));
            surface.DrawBox(box, _BorderStyle, _Title);

            int contentX = boxX + 1 + pad.Left;
            int contentY = boxY + 1 + pad.Top;
            for (int i = 0; i < messageLines.Count; i++)
                surface.DrawStyledText(contentX, contentY + i, messageLines[i]);

            int buttonsRow = contentY + messageLines.Count + 1;
            RenderButtons(surface, contentX, buttonsRow, contentWidth);
        }

        private void RenderButtons(ISurface surface, int startX, int row, int width)
        {
            int totalWidth = 0;
            for (int i = 0; i < _Buttons.Length; i++)
                totalWidth += _Buttons[i].Length + 4 + 1;

            int cursor = startX + Math.Max(0, (width - totalWidth) / 2);
            for (int i = 0; i < _Buttons.Length; i++)
            {
                string label = "[ " + _Buttons[i] + " ]";
                CellStyle style = i == _Selected ? _SelectedStyle : CellStyle.Default;
                cursor += surface.DrawText(cursor, row, label, style);
                cursor += 1;
            }
        }
    }
}

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

            int inner = Math.Min(screenWidth - 4, Math.Max(30, TUIKit.Unicode.Graphemes.MeasureWidth(_Message) + 2));
            if (inner < 10)
                inner = Math.Max(10, screenWidth - 4);

            IReadOnlyList<StyledText> messageLines = TextWrapper.Wrap(Text.From(_Message), inner);
            int boxWidth = inner + 2;
            int boxHeight = messageLines.Count + 4;

            int boxX = Math.Max(0, (screenWidth - boxWidth) / 2);
            int boxY = Math.Max(0, (screenHeight - boxHeight) / 2);
            Rect box = new Rect(boxX, boxY, boxWidth, boxHeight);

            surface.Fill(box, Cell.Blank(CellStyle.Default));
            surface.DrawBox(box, _BorderStyle, _Title);

            for (int i = 0; i < messageLines.Count; i++)
                surface.DrawStyledText(boxX + 1, boxY + 1 + i, messageLines[i]);

            RenderButtons(surface, boxX, boxY + boxHeight - 2, boxWidth);
        }

        private void RenderButtons(ISurface surface, int boxX, int row, int boxWidth)
        {
            int totalWidth = 0;
            for (int i = 0; i < _Buttons.Length; i++)
                totalWidth += _Buttons[i].Length + 4 + 1;

            int cursor = boxX + Math.Max(1, (boxWidth - totalWidth) / 2);
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

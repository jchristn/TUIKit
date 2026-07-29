namespace TUIKit.Testing
{
    using System;
    using System.Text;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Widgets;

    /// <summary>
    /// A fluent, headless test harness for a single widget. It owns an off-screen buffer, feeds keys
    /// to the widget when it is focusable, renders on demand, and exposes the resulting text for
    /// assertions — no real terminal required, so widget tests run deterministically anywhere. Chain
    /// calls: <c>WidgetTester.For(list, 20, 5).Press(KeyCode.Down).Render().AssertContains("item")</c>.
    /// </summary>
    public sealed class WidgetTester
    {
        private readonly IWidget _Widget;
        private readonly IFocusable? _Focusable;
        private readonly CellBuffer _Buffer;

        /// <summary>
        /// Initializes a new instance of the <see cref="WidgetTester"/> class.
        /// </summary>
        /// <param name="widget">The widget under test. Must not be null.</param>
        /// <param name="width">The buffer width in cells. Must be positive.</param>
        /// <param name="height">The buffer height in cells. Must be positive.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="widget"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a dimension is not positive.</exception>
        public WidgetTester(IWidget widget, int width, int height)
        {
            if (widget == null)
                throw new ArgumentNullException(nameof(widget));
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height));

            _Widget = widget;
            _Focusable = widget as IFocusable;
            _Buffer = new CellBuffer(width, height);
        }

        /// <summary>
        /// Creates a tester for a widget.
        /// </summary>
        /// <param name="widget">The widget under test. Must not be null.</param>
        /// <param name="width">The buffer width in cells. Must be positive.</param>
        /// <param name="height">The buffer height in cells. Must be positive.</param>
        /// <returns>A new tester.</returns>
        public static WidgetTester For(IWidget widget, int width, int height)
        {
            return new WidgetTester(widget, width, height);
        }

        /// <summary>
        /// Gets whether the most recent key press was consumed by the widget.
        /// </summary>
        public bool LastKeyHandled { get; private set; }

        /// <summary>
        /// Sends a special key to the widget (when focusable).
        /// </summary>
        /// <param name="code">The key code.</param>
        /// <returns>This tester, for chaining.</returns>
        public WidgetTester Press(KeyCode code)
        {
            return Send(KeyEvent.Special(code));
        }

        /// <summary>
        /// Sends a key event to the widget (when focusable).
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns>This tester, for chaining.</returns>
        public WidgetTester Press(KeyEvent key)
        {
            return Send(key);
        }

        /// <summary>
        /// Types a run of characters, sending one key event per character.
        /// </summary>
        /// <param name="text">The text to type. Must not be null.</param>
        /// <returns>This tester, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public WidgetTester Type(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            foreach (char c in text)
                Send(KeyEvent.Char(c));

            return this;
        }

        /// <summary>
        /// Clears the buffer and re-renders the widget.
        /// </summary>
        /// <returns>This tester, for chaining.</returns>
        public WidgetTester Render()
        {
            _Buffer.Clear(CellStyle.Default);
            _Widget.Render(new BufferSurface(_Buffer));
            return this;
        }

        /// <summary>
        /// Gets the text of a single rendered row, with trailing blanks trimmed.
        /// </summary>
        /// <param name="row">The zero-based row index.</param>
        /// <returns>The row text.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the row is out of range.</exception>
        public string Row(int row)
        {
            if (row < 0 || row >= _Buffer.Height)
                throw new ArgumentOutOfRangeException(nameof(row));

            StringBuilder builder = new StringBuilder();
            for (int x = 0; x < _Buffer.Width; x++)
                builder.Append(_Buffer.Get(x, row).Grapheme);

            return builder.ToString().TrimEnd();
        }

        /// <summary>
        /// Gets the whole rendered surface as newline-joined rows.
        /// </summary>
        /// <returns>The rendered text.</returns>
        public string Text()
        {
            StringBuilder builder = new StringBuilder();
            for (int y = 0; y < _Buffer.Height; y++)
            {
                if (y > 0)
                    builder.Append('\n');
                builder.Append(Row(y));
            }

            return builder.ToString();
        }

        /// <summary>
        /// Gets whether the rendered text contains a substring.
        /// </summary>
        /// <param name="text">The substring. Must not be null.</param>
        /// <returns><c>true</c> when present; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public bool Contains(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            return Text().Contains(text);
        }

        /// <summary>
        /// Asserts that the rendered text contains a substring.
        /// </summary>
        /// <param name="text">The expected substring. Must not be null.</param>
        /// <returns>This tester, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the text is absent.</exception>
        public WidgetTester AssertContains(string text)
        {
            if (!Contains(text))
                throw new InvalidOperationException("Expected rendered output to contain '" + text + "' but it did not. Output:\n" + Text());

            return this;
        }

        /// <summary>
        /// Asserts that the rendered text does not contain a substring.
        /// </summary>
        /// <param name="text">The forbidden substring. Must not be null.</param>
        /// <returns>This tester, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the text is present.</exception>
        public WidgetTester AssertNotContains(string text)
        {
            if (Contains(text))
                throw new InvalidOperationException("Expected rendered output NOT to contain '" + text + "' but it did. Output:\n" + Text());

            return this;
        }

        private WidgetTester Send(KeyEvent key)
        {
            LastKeyHandled = _Focusable != null && _Focusable.HandleKey(key);
            return this;
        }
    }
}

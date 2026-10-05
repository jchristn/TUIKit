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

        /// <summary>
        /// Gets whether the most recent mouse event was consumed by the widget.
        /// </summary>
        public bool LastMouseHandled { get; private set; }

        /// <summary>
        /// Gets the rendered cell at a position, so a test can assert on glyphs and styles rather than
        /// text alone. Call <see cref="Render"/> first.
        /// </summary>
        /// <param name="x">The zero-based column. Must lie within the buffer width.</param>
        /// <param name="y">The zero-based row. Must lie within the buffer height.</param>
        /// <returns>The cell.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the position lies outside the buffer.</exception>
        public Cell CellAt(int x, int y)
        {
            RequireInside(x, y);
            return _Buffer.Get(x, y);
        }

        /// <summary>
        /// Delivers a mouse event to the widget in the widget's own coordinates. This exercises the
        /// widget's <see cref="IMouseAware"/> handling directly; it does not involve a host hit map. To
        /// test routing through a real application, feed input with
        /// <see cref="Terminal.HeadlessBackend.FeedClick"/> and pump the application instead.
        /// </summary>
        /// <param name="mouse">The event, in widget-local coordinates. Must not be null.</param>
        /// <returns>This tester, for chaining. <see cref="LastMouseHandled"/> reports the result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mouse"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the position lies outside the buffer.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the widget does not implement <see cref="IMouseAware"/>.</exception>
        public WidgetTester Mouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            RequireInside(mouse.X, mouse.Y);
            if (!(_Widget is IMouseAware aware))
                throw new InvalidOperationException("The widget under test (" + _Widget.GetType().Name + ") does not handle the mouse.");

            LastMouseHandled = aware.HandleMouse(mouse);
            return this;
        }

        /// <summary>
        /// Delivers a press and a release at one cell (a single click) in widget-local coordinates.
        /// <see cref="LastMouseHandled"/> reports whether the press was consumed.
        /// </summary>
        /// <param name="x">The zero-based column. Must lie within the buffer width.</param>
        /// <param name="y">The zero-based row. Must lie within the buffer height.</param>
        /// <param name="button">The button. Defaults to <see cref="MouseButton.Left"/>.</param>
        /// <returns>This tester, for chaining.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the position lies outside the buffer.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the widget does not implement <see cref="IMouseAware"/>.</exception>
        public WidgetTester Click(int x, int y, MouseButton button = MouseButton.Left)
        {
            return ClickWithCount(x, y, button, 1);
        }

        /// <summary>
        /// Delivers two clicks at one cell, the second press stamped with a click count of two.
        /// </summary>
        /// <param name="x">The zero-based column. Must lie within the buffer width.</param>
        /// <param name="y">The zero-based row. Must lie within the buffer height.</param>
        /// <returns>This tester, for chaining.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the position lies outside the buffer.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the widget does not implement <see cref="IMouseAware"/>.</exception>
        public WidgetTester DoubleClick(int x, int y)
        {
            ClickWithCount(x, y, MouseButton.Left, 1);
            return ClickWithCount(x, y, MouseButton.Left, 2);
        }

        /// <summary>
        /// Delivers wheel notches at one cell.
        /// </summary>
        /// <param name="x">The zero-based column. Must lie within the buffer width.</param>
        /// <param name="y">The zero-based row. Must lie within the buffer height.</param>
        /// <param name="delta">The notch count: negative scrolls up, positive scrolls down. Must not be zero.</param>
        /// <returns>This tester, for chaining.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the position lies outside the buffer or
        /// <paramref name="delta"/> is zero.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the widget does not implement <see cref="IMouseAware"/>.</exception>
        public WidgetTester Wheel(int x, int y, int delta)
        {
            if (delta == 0)
                throw new ArgumentOutOfRangeException(nameof(delta), delta, "Wheel delta must not be zero.");

            MouseButton button = delta < 0 ? MouseButton.WheelUp : MouseButton.WheelDown;
            for (int i = 0; i < Math.Abs(delta); i++)
                Mouse(new MouseEvent(MouseEventKind.Wheel, button, x, y, KeyModifiers.None, 0));

            return this;
        }

        /// <summary>
        /// Delivers a pointer move with no button held (hover).
        /// </summary>
        /// <param name="x">The zero-based column. Must lie within the buffer width.</param>
        /// <param name="y">The zero-based row. Must lie within the buffer height.</param>
        /// <returns>This tester, for chaining.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the position lies outside the buffer.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the widget does not implement <see cref="IMouseAware"/>.</exception>
        public WidgetTester Move(int x, int y)
        {
            return Mouse(new MouseEvent(MouseEventKind.Move, MouseButton.None, x, y, KeyModifiers.None, 0));
        }

        /// <summary>
        /// Delivers a drag: a press at the start cell, a button-held move to the end cell, and a release.
        /// </summary>
        /// <param name="fromX">The zero-based start column.</param>
        /// <param name="fromY">The zero-based start row.</param>
        /// <param name="toX">The zero-based end column.</param>
        /// <param name="toY">The zero-based end row.</param>
        /// <returns>This tester, for chaining.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when either position lies outside the buffer.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the widget does not implement <see cref="IMouseAware"/>.</exception>
        public WidgetTester Drag(int fromX, int fromY, int toX, int toY)
        {
            RequireInside(toX, toY);
            Mouse(new MouseEvent(MouseEventKind.Press, MouseButton.Left, fromX, fromY, KeyModifiers.None, 1));
            Mouse(new MouseEvent(MouseEventKind.Move, MouseButton.Left, toX, toY, KeyModifiers.None, 0));
            return Mouse(new MouseEvent(MouseEventKind.Release, MouseButton.Left, toX, toY, KeyModifiers.None, 0));
        }

        private WidgetTester ClickWithCount(int x, int y, MouseButton button, int clickCount)
        {
            Mouse(new MouseEvent(MouseEventKind.Press, button, x, y, KeyModifiers.None, clickCount));
            bool pressHandled = LastMouseHandled;
            Mouse(new MouseEvent(MouseEventKind.Release, button, x, y, KeyModifiers.None, 0));
            LastMouseHandled = pressHandled;
            return this;
        }

        private void RequireInside(int x, int y)
        {
            if (x < 0 || x >= _Buffer.Width)
                throw new ArgumentOutOfRangeException(nameof(x), x, "Column must lie within the tester width of " + _Buffer.Width + ".");
            if (y < 0 || y >= _Buffer.Height)
                throw new ArgumentOutOfRangeException(nameof(y), y, "Row must lie within the tester height of " + _Buffer.Height + ".");
        }

        private WidgetTester Send(KeyEvent key)
        {
            LastKeyHandled = _Focusable != null && _Focusable.HandleKey(key);
            return this;
        }
    }
}

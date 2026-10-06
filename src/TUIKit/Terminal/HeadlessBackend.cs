namespace TUIKit.Terminal
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// An in-memory <see cref="ITerminalBackend"/> used for tests and snapshotting. All output is
    /// captured as a string, input is supplied programmatically, and the size can be changed to
    /// simulate a resize. Nothing touches a real console, so behavior is fully deterministic.
    /// </summary>
    /// <remarks>This type is thread-safe for concurrent writes and reads.</remarks>
    public sealed class HeadlessBackend : ITerminalBackend, ISharedTerminalBackend
    {
        private readonly object _Sync = new object();
        private readonly StringBuilder _Output = new StringBuilder();
        private readonly Queue<byte> _Input = new Queue<byte>();
        private TerminalCapabilities _Capabilities;
        private Size _Size;
        private bool _Interactive;
        private bool _Started;
        private bool _Stopped;
        private bool _ClaimsTerminal = true;

        /// <inheritdoc/>
        public TerminalCapabilities Capabilities
        {
            get { lock (_Sync) { return _Capabilities; } }
        }

        /// <inheritdoc/>
        public Size Size
        {
            get { lock (_Sync) { return _Size; } }
        }

        /// <inheritdoc/>
        public bool IsInteractive
        {
            get { lock (_Sync) { return _Interactive; } }
        }

        /// <summary>
        /// Gets or sets a value indicating whether an application started on this backend takes the
        /// process's one terminal slot (see <see cref="ISharedTerminalBackend"/>). Defaults to true, the
        /// 1.4.0 behavior, so a second application in the same process still throws on start. Set false
        /// before starting to run several headless applications side by side, for example in parallel
        /// tests; those applications also skip the process-wide session telemetry and safety net. Safe
        /// to read from any thread.
        /// </summary>
        public bool ClaimsTerminal
        {
            get { lock (_Sync) { return _ClaimsTerminal; } }
            set { lock (_Sync) { _ClaimsTerminal = value; } }
        }

        /// <summary>
        /// Gets a value indicating whether <see cref="Start"/> has been called.
        /// </summary>
        public bool IsStarted
        {
            get { lock (_Sync) { return _Started; } }
        }

        /// <summary>
        /// Gets a value indicating whether <see cref="Stop"/> has been called.
        /// </summary>
        public bool IsStopped
        {
            get { lock (_Sync) { return _Stopped; } }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HeadlessBackend"/> class.
        /// </summary>
        /// <param name="width">The initial width in cells. Must be greater than zero. Defaults to 80.</param>
        /// <param name="height">The initial height in cells. Must be greater than zero. Defaults to 24.</param>
        /// <param name="capabilities">The reported capabilities, or null for <see cref="TerminalCapabilities.Full"/>.</param>
        /// <param name="interactive">Whether the backend reports as interactive. Defaults to true.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a dimension is not positive.</exception>
        public HeadlessBackend(int width = 80, int height = 24, TerminalCapabilities? capabilities = null, bool interactive = true)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be greater than zero.");
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be greater than zero.");

            _Size = new Size(width, height);
            _Capabilities = capabilities ?? TerminalCapabilities.Full;
            _Interactive = interactive;
        }

        /// <inheritdoc/>
        public void Start()
        {
            lock (_Sync) { _Started = true; }
        }

        /// <inheritdoc/>
        public void Write(string data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            lock (_Sync) { _Output.Append(data); }
        }

        /// <inheritdoc/>
        public void Flush()
        {
        }

        /// <inheritdoc/>
        public int ReadInput(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            lock (_Sync)
            {
                int read = 0;
                while (read < count && _Input.Count > 0)
                {
                    buffer[offset + read] = _Input.Dequeue();
                    read++;
                }

                return read;
            }
        }

        /// <inheritdoc/>
        public void Stop()
        {
            lock (_Sync) { _Stopped = true; }
        }

        /// <summary>
        /// Returns all output captured so far and clears the capture buffer.
        /// </summary>
        /// <returns>The captured output. Never null.</returns>
        public string TakeOutput()
        {
            lock (_Sync)
            {
                string result = _Output.ToString();
                _Output.Clear();
                return result;
            }
        }

        /// <summary>
        /// Returns all output captured so far without clearing it.
        /// </summary>
        /// <returns>The captured output. Never null.</returns>
        public string PeekOutput()
        {
            lock (_Sync) { return _Output.ToString(); }
        }

        /// <summary>
        /// Queues raw UTF-8 input bytes to be returned by <see cref="ReadInput"/>.
        /// </summary>
        /// <param name="text">The input text. Must not be null.</param>
        public void FeedInput(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            byte[] bytes = Encoding.UTF8.GetBytes(text);
            FeedInput(bytes);
        }

        /// <summary>
        /// Queues raw input bytes to be returned by <see cref="ReadInput"/>.
        /// </summary>
        /// <param name="bytes">The input bytes. Must not be null.</param>
        public void FeedInput(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            lock (_Sync)
            {
                for (int i = 0; i < bytes.Length; i++)
                    _Input.Enqueue(bytes[i]);
            }
        }

        /// <summary>
        /// Queues the terminal input sequence for a key, encoded with
        /// <see cref="Input.KeySequenceEncoder"/>, so tests can press any key (F1 through F12, modified
        /// arrows, Ctrl and Alt chords) without writing escape sequences.
        /// </summary>
        /// <param name="key">The key event.</param>
        public void FeedKey(Input.KeyEvent key)
        {
            FeedInput(Input.KeySequenceEncoder.Encode(key));
        }

        /// <summary>
        /// Queues the terminal input sequence for a chord written in <see cref="Input.KeyChord.Parse"/>
        /// syntax, for example <c>"f9"</c> or <c>"ctrl+p"</c>.
        /// </summary>
        /// <param name="chord">The chord text. Must not be null or empty.</param>
        /// <exception cref="ArgumentException">Thrown when the chord cannot be parsed.</exception>
        public void FeedKey(string chord)
        {
            FeedInput(Input.KeySequenceEncoder.Encode(chord));
        }

        /// <summary>
        /// Queues the SGR terminal report for a mouse event, encoded with
        /// <see cref="Input.MouseSequenceEncoder"/>. The host decodes it with its real parser and routes
        /// it through the hit map, click synthesis, hover, and focus-on-click, exactly as it would a
        /// user's pointer. Render a frame first (for example <c>TuiApplication.RenderOnce</c>) so the hit
        /// map reflects the current layout, then pump input.
        /// </summary>
        /// <param name="mouse">The event, in zero-based screen coordinates. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mouse"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the position lies outside the backend's
        /// current size.</exception>
        /// <exception cref="ArgumentException">Thrown when the event has no wire encoding (see
        /// <see cref="Input.MouseSequenceEncoder.Encode"/>).</exception>
        public void FeedMouse(Input.MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            RequireOnScreen(mouse.X, mouse.Y);
            FeedInput(Input.MouseSequenceEncoder.Encode(mouse));
        }

        /// <summary>
        /// Queues a press and release at one cell: a single click.
        /// </summary>
        /// <param name="x">The zero-based column. Must lie within the backend's width.</param>
        /// <param name="y">The zero-based row. Must lie within the backend's height.</param>
        /// <param name="button">The button. Defaults to <see cref="Input.MouseButton.Left"/>; must be left,
        /// middle, or right.</param>
        /// <param name="modifiers">Modifier keys held during the click. Defaults to none.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the position lies outside the screen.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="button"/> is not a click button.</exception>
        public void FeedClick(int x, int y, Input.MouseButton button = Input.MouseButton.Left, Input.KeyModifiers modifiers = Input.KeyModifiers.None)
        {
            RequireOnScreen(x, y);
            FeedInput(Input.MouseSequenceEncoder.EncodeClick(x, y, button, modifiers));
        }

        /// <summary>
        /// Queues two left clicks at one cell. Pumped together, the host's click synthesizer stamps the
        /// second press with a click count of two.
        /// </summary>
        /// <param name="x">The zero-based column. Must lie within the backend's width.</param>
        /// <param name="y">The zero-based row. Must lie within the backend's height.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the position lies outside the screen.</exception>
        public void FeedDoubleClick(int x, int y)
        {
            RequireOnScreen(x, y);
            string click = Input.MouseSequenceEncoder.EncodeClick(x, y);
            FeedInput(click + click);
        }

        /// <summary>
        /// Queues wheel notches at one cell.
        /// </summary>
        /// <param name="x">The zero-based column. Must lie within the backend's width.</param>
        /// <param name="y">The zero-based row. Must lie within the backend's height.</param>
        /// <param name="delta">The notch count: negative scrolls up, positive scrolls down. Must not be zero.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the position lies outside the screen or
        /// <paramref name="delta"/> is zero.</exception>
        public void FeedWheel(int x, int y, int delta)
        {
            if (delta == 0)
                throw new ArgumentOutOfRangeException(nameof(delta), delta, "Wheel delta must not be zero.");

            RequireOnScreen(x, y);
            Input.MouseButton button = delta < 0 ? Input.MouseButton.WheelUp : Input.MouseButton.WheelDown;
            int notches = Math.Abs(delta);
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < notches; i++)
                builder.Append(Input.MouseSequenceEncoder.Encode(new Input.MouseEvent(Input.MouseEventKind.Wheel, button, x, y, Input.KeyModifiers.None, 0)));

            FeedInput(builder.ToString());
        }

        /// <summary>
        /// Queues a pointer move with no button held (hover).
        /// </summary>
        /// <param name="x">The zero-based column. Must lie within the backend's width.</param>
        /// <param name="y">The zero-based row. Must lie within the backend's height.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the position lies outside the screen.</exception>
        public void FeedMove(int x, int y)
        {
            RequireOnScreen(x, y);
            FeedInput(Input.MouseSequenceEncoder.Encode(new Input.MouseEvent(Input.MouseEventKind.Move, Input.MouseButton.None, x, y, Input.KeyModifiers.None, 0)));
        }

        /// <summary>
        /// Queues a drag: a press at the start cell, a button-held move to the end cell, and a release
        /// there.
        /// </summary>
        /// <param name="fromX">The zero-based start column. Must lie within the backend's width.</param>
        /// <param name="fromY">The zero-based start row. Must lie within the backend's height.</param>
        /// <param name="toX">The zero-based end column. Must lie within the backend's width.</param>
        /// <param name="toY">The zero-based end row. Must lie within the backend's height.</param>
        /// <param name="button">The button held. Defaults to <see cref="Input.MouseButton.Left"/>; must be
        /// left, middle, or right.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when either position lies outside the screen.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="button"/> is not a click button.</exception>
        public void FeedDrag(int fromX, int fromY, int toX, int toY, Input.MouseButton button = Input.MouseButton.Left)
        {
            RequireOnScreen(fromX, fromY);
            RequireOnScreen(toX, toY);
            FeedInput(
                Input.MouseSequenceEncoder.Encode(new Input.MouseEvent(Input.MouseEventKind.Press, button, fromX, fromY, Input.KeyModifiers.None, 1))
                + Input.MouseSequenceEncoder.Encode(new Input.MouseEvent(Input.MouseEventKind.Move, button, toX, toY, Input.KeyModifiers.None, 0))
                + Input.MouseSequenceEncoder.Encode(new Input.MouseEvent(Input.MouseEventKind.Release, button, toX, toY, Input.KeyModifiers.None, 0)));
        }

        /// <summary>
        /// Simulates a terminal resize.
        /// </summary>
        /// <param name="width">The new width in cells. Must be greater than zero.</param>
        /// <param name="height">The new height in cells. Must be greater than zero.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a dimension is not positive.</exception>
        public void Resize(int width, int height)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be greater than zero.");
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be greater than zero.");

            lock (_Sync) { _Size = new Size(width, height); }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Stop();
        }

        private void RequireOnScreen(int x, int y)
        {
            Size size = Size;
            if (x < 0 || x >= size.Width)
                throw new ArgumentOutOfRangeException(nameof(x), x, "Column must lie within the screen width of " + size.Width + ".");
            if (y < 0 || y >= size.Height)
                throw new ArgumentOutOfRangeException(nameof(y), y, "Row must lie within the screen height of " + size.Height + ".");
        }
    }
}

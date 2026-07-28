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
    public sealed class HeadlessBackend : ITerminalBackend
    {
        private readonly object _Sync = new object();
        private readonly StringBuilder _Output = new StringBuilder();
        private readonly Queue<byte> _Input = new Queue<byte>();
        private TerminalCapabilities _Capabilities;
        private Size _Size;
        private bool _Interactive;
        private bool _Started;
        private bool _Stopped;

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
    }
}

namespace Test.Shared.Suites
{
    using System;
    using TUIKit;
    using TUIKit.Terminal;

    /// <summary>
    /// A test backend that wraps a <see cref="HeadlessBackend"/> but implements only
    /// <see cref="ITerminalBackend"/>, the way a third-party backend written before 1.5.0 does, so tests
    /// can check that such a backend is treated as claiming the terminal.
    /// </summary>
    public sealed class PlainBackend : ITerminalBackend
    {
        private readonly HeadlessBackend _Inner;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlainBackend"/> class.
        /// </summary>
        /// <param name="inner">The wrapped backend. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="inner"/> is null.</exception>
        public PlainBackend(HeadlessBackend inner)
        {
            _Inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        /// <inheritdoc/>
        public TerminalCapabilities Capabilities
        {
            get { return _Inner.Capabilities; }
        }

        /// <inheritdoc/>
        public Size Size
        {
            get { return _Inner.Size; }
        }

        /// <inheritdoc/>
        public bool IsInteractive
        {
            get { return _Inner.IsInteractive; }
        }

        /// <inheritdoc/>
        public void Start()
        {
            _Inner.Start();
        }

        /// <inheritdoc/>
        public void Write(string data)
        {
            _Inner.Write(data);
        }

        /// <inheritdoc/>
        public void Flush()
        {
            _Inner.Flush();
        }

        /// <inheritdoc/>
        public int ReadInput(byte[] buffer, int offset, int count)
        {
            return _Inner.ReadInput(buffer, offset, count);
        }

        /// <inheritdoc/>
        public void Stop()
        {
            _Inner.Stop();
        }

        /// <summary>
        /// Disposes the wrapped backend.
        /// </summary>
        public void Dispose()
        {
            _Inner.Dispose();
        }
    }
}

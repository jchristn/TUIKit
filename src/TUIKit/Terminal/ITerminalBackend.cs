namespace TUIKit.Terminal
{
    using System;

    /// <summary>
    /// The lowest-level surface TUIKit renders through: a raw sink for output bytes, a source of raw
    /// input bytes, and a report of the current size and capabilities. Concrete backends wrap a real
    /// console or capture everything in memory for tests.
    /// </summary>
    /// <remarks>
    /// The terminal is a singleton resource. Output writes are expected to occur on the render thread;
    /// implementations are not required to be thread-safe beyond what their documentation states.
    /// </remarks>
    public interface ITerminalBackend : IDisposable
    {
        /// <summary>
        /// Gets the terminal capabilities reported by this backend.
        /// </summary>
        TerminalCapabilities Capabilities { get; }

        /// <summary>
        /// Gets the current size of the terminal in cells.
        /// </summary>
        Size Size { get; }

        /// <summary>
        /// Gets a value indicating whether the backend is attached to an interactive terminal. When
        /// false the host should degrade to plain line output rather than emit escape sequences.
        /// </summary>
        bool IsInteractive { get; }

        /// <summary>
        /// Prepares the terminal for rendering (for example enabling raw mode and virtual terminal
        /// processing). Safe to call once before the first write.
        /// </summary>
        void Start();

        /// <summary>
        /// Writes a run of already-encoded output (text and escape sequences) to the terminal. The
        /// data is buffered until <see cref="Flush"/> is called.
        /// </summary>
        /// <param name="data">The output to write. Must not be null.</param>
        void Write(string data);

        /// <summary>
        /// Flushes buffered output to the underlying device.
        /// </summary>
        void Flush();

        /// <summary>
        /// Reads available raw input bytes without blocking beyond a short poll interval.
        /// </summary>
        /// <param name="buffer">The destination buffer. Must not be null.</param>
        /// <param name="offset">The zero-based offset into the buffer.</param>
        /// <param name="count">The maximum number of bytes to read.</param>
        /// <returns>The number of bytes read, or zero when no input is currently available.</returns>
        int ReadInput(byte[] buffer, int offset, int count);

        /// <summary>
        /// Restores the terminal to its original mode (raw mode disabled, virtual terminal state
        /// left as it was found). Safe to call multiple times.
        /// </summary>
        void Stop();
    }
}

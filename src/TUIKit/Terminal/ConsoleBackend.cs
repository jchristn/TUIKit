namespace TUIKit.Terminal
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;

    /// <summary>
    /// An <see cref="ITerminalBackend"/> backed by the real process console. On Windows it enables
    /// virtual terminal processing and raw input through the console API and reads and writes through
    /// <see cref="Console"/>. On Unix-like systems it puts the terminal into raw mode in-process
    /// through the libc <c>termios</c> API and performs all input and output directly on the standard
    /// file descriptors (see <see cref="PosixTerminal"/>), bypassing <see cref="Console"/> entirely —
    /// the Unix <see cref="Console"/> implementation otherwise echoes keystrokes and re-cooks the
    /// terminal behind the raw-mode settings. A background thread reads raw input bytes so
    /// <see cref="ReadInput"/> never blocks the render loop.
    /// </summary>
    /// <remarks>
    /// The terminal is a singleton resource; create at most one instance per process. Output writes
    /// are expected on a single thread.
    /// </remarks>
    public sealed class ConsoleBackend : ITerminalBackend
    {
        private readonly bool _IsWindows;
        private readonly bool _Interactive;
        private readonly TerminalCapabilities _Capabilities;
        private readonly ConcurrentQueue<byte> _InputQueue = new ConcurrentQueue<byte>();
        private readonly StringBuilder _WriteBuffer = new StringBuilder();
        private readonly object _WriteSync = new object();

        private Stream? _OutputStream;
        private Stream? _InputStream;
        private Thread? _ReaderThread;
        private volatile bool _Running;
        private uint _OriginalOutputMode;
        private uint _OriginalInputMode;
        private bool _RestoredWindowsMode = true;
        private byte[]? _SavedTermios;
        private EventHandler? _ProcessExitHandler;
        private int _ExitHandled;
        private bool _Disposed;

        /// <inheritdoc/>
        public TerminalCapabilities Capabilities
        {
            get { return _Capabilities; }
        }

        /// <inheritdoc/>
        public Size Size
        {
            get
            {
                // Console.WindowWidth/Height query the tty size via a platform-correct ioctl and,
                // unlike Console's stdin/stdout streams, do not re-cook the terminal on Unix. (A direct
                // ioctl P/Invoke is avoided because ioctl is variadic and unsafe to call that way on
                // Apple Silicon.)
                int width;
                int height;
                try
                {
                    width = Console.WindowWidth;
                    height = Console.WindowHeight;
                }
                catch (IOException)
                {
                    width = 0;
                    height = 0;
                }
                catch (ArgumentOutOfRangeException)
                {
                    width = 0;
                    height = 0;
                }

                if (width <= 0)
                    width = 80;
                if (height <= 0)
                    height = 24;

                return new Size(width, height);
            }
        }

        /// <inheritdoc/>
        public bool IsInteractive
        {
            get { return _Interactive; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConsoleBackend"/> class, detecting
        /// interactivity and capabilities from the environment.
        /// </summary>
        public ConsoleBackend()
        {
            _IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            _Interactive = !Console.IsOutputRedirected && !Console.IsInputRedirected;
            _Capabilities = CapabilityDetector.Detect(_Interactive);
        }

        /// <inheritdoc/>
        public void Start()
        {
            if (_Running)
                return;

            _Running = true;

            if (_IsWindows)
            {
                // Windows: Console is well-behaved and provides the VT-enabled streams.
                try
                {
                    Console.OutputEncoding = new UTF8Encoding(false);
                }
                catch (IOException)
                {
                    // Non-interactive or redirected output; encoding cannot be set.
                }

                _OutputStream = Console.OpenStandardOutput();
                _InputStream = Console.OpenStandardInput();
            }

            // Unix deliberately does not touch System.Console: it performs I/O on the raw file
            // descriptors so its echo/line-discipline management cannot fight the termios raw mode.

            if (!_Interactive)
                return;

            if (_IsWindows)
                EnableWindowsRawMode();
            else
                _SavedTermios = PosixTerminal.EnterRawMode();

            // Best-effort net so an abnormal exit (an unhandled exception, or a Ctrl+C the runtime turns
            // into an abrupt process kill) that bypasses Stop does not leave the terminal in raw mode
            // with the cursor hidden and mouse reporting on. Installed on every interactive platform.
            Interlocked.Exchange(ref _ExitHandled, 0);
            _ProcessExitHandler = OnProcessExit;
            AppDomain.CurrentDomain.ProcessExit += _ProcessExitHandler;

            _ReaderThread = new Thread(ReaderLoop)
            {
                IsBackground = true,
                Name = "TUIKit.ConsoleInput"
            };
            _ReaderThread.Start();
        }

        /// <inheritdoc/>
        public void Write(string data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            lock (_WriteSync)
                _WriteBuffer.Append(data);
        }

        /// <inheritdoc/>
        public void Flush()
        {
            string pending;
            lock (_WriteSync)
            {
                if (_WriteBuffer.Length == 0)
                    return;

                pending = _WriteBuffer.ToString();
                _WriteBuffer.Clear();
            }

            byte[] bytes = Encoding.UTF8.GetBytes(pending);
            if (_IsWindows)
            {
                if (_OutputStream == null)
                    return;

                _OutputStream.Write(bytes, 0, bytes.Length);
                _OutputStream.Flush();
            }
            else
            {
                PosixTerminal.WriteStdout(bytes, bytes.Length);
            }
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

            int read = 0;
            while (read < count && _InputQueue.TryDequeue(out byte value))
            {
                buffer[offset + read] = value;
                read++;
            }

            return read;
        }

        /// <inheritdoc/>
        public void Stop()
        {
            if (!_Running)
                return;

            _Running = false;

            try
            {
                Flush();
            }
            catch (IOException)
            {
                // Best effort during teardown.
            }

            if (_Interactive)
            {
                // Suppress the process-exit net: a clean Stop has taken responsibility for the restore.
                Interlocked.Exchange(ref _ExitHandled, 1);

                if (_ProcessExitHandler != null)
                {
                    AppDomain.CurrentDomain.ProcessExit -= _ProcessExitHandler;
                    _ProcessExitHandler = null;
                }

                if (_IsWindows)
                {
                    RestoreWindowsMode();
                }
                else
                {
                    PosixTerminal.RestoreMode(_SavedTermios);
                    _SavedTermios = null;
                }
            }
        }

        private void EnableWindowsRawMode()
        {
            IntPtr outputHandle = NativeConsole.GetStdHandle(NativeConsole.StdOutputHandle);
            IntPtr inputHandle = NativeConsole.GetStdHandle(NativeConsole.StdInputHandle);

            if (NativeConsole.GetConsoleMode(outputHandle, out uint outMode)
                && NativeConsole.GetConsoleMode(inputHandle, out uint inMode))
            {
                _OriginalOutputMode = outMode;
                _OriginalInputMode = inMode;
                _RestoredWindowsMode = false;

                NativeConsole.SetConsoleMode(outputHandle, outMode | NativeConsole.EnableVirtualTerminalProcessing);

                uint rawInput = inMode & ~(NativeConsole.EnableLineInput
                    | NativeConsole.EnableEchoInput
                    | NativeConsole.EnableProcessedInput);
                rawInput |= NativeConsole.EnableVirtualTerminalInput;
                NativeConsole.SetConsoleMode(inputHandle, rawInput);
            }
        }

        private void RestoreWindowsMode()
        {
            if (_RestoredWindowsMode)
                return;

            IntPtr outputHandle = NativeConsole.GetStdHandle(NativeConsole.StdOutputHandle);
            IntPtr inputHandle = NativeConsole.GetStdHandle(NativeConsole.StdInputHandle);
            NativeConsole.SetConsoleMode(outputHandle, _OriginalOutputMode);
            NativeConsole.SetConsoleMode(inputHandle, _OriginalInputMode);
            _RestoredWindowsMode = true;
        }

        private void OnProcessExit(object? sender, EventArgs e)
        {
            if (Interlocked.Exchange(ref _ExitHandled, 1) != 0)
                return;

            // Undo the visual state a running session leaves behind — mouse reporting and bracketed
            // paste first, then the cursor and the alternate screen — so a process that exits without
            // calling Stop still hands back a usable terminal. Written before the mode restore so the
            // escapes go out while the output path is still in its running configuration.
            try
            {
                byte[] reset = Encoding.UTF8.GetBytes(
                    Ansi.DisableMouse + Ansi.DisableBracketedPaste
                    + Ansi.ShowCursor + Ansi.ExitAltScreen + Ansi.ResetAttributes);
                WriteReset(reset);
            }
            catch (IOException)
            {
                // Best effort during teardown; the mode restore below still runs.
            }

            if (_IsWindows)
            {
                RestoreWindowsMode();
            }
            else
            {
                PosixTerminal.RestoreMode(_SavedTermios);
                _SavedTermios = null;
            }
        }

        private void WriteReset(byte[] bytes)
        {
            if (_IsWindows)
            {
                if (_OutputStream == null)
                    return;

                _OutputStream.Write(bytes, 0, bytes.Length);
                _OutputStream.Flush();
            }
            else
            {
                PosixTerminal.WriteStdout(bytes, bytes.Length);
            }
        }

        private void ReaderLoop()
        {
            byte[] buffer = new byte[1024];
            while (_Running)
            {
                int read;
                try
                {
                    read = _IsWindows
                        ? (_InputStream != null ? _InputStream.Read(buffer, 0, buffer.Length) : -1)
                        : PosixTerminal.ReadStdin(buffer, buffer.Length);
                }
                catch (IOException)
                {
                    break;
                }

                if (read <= 0)
                {
                    Thread.Sleep(5);
                    continue;
                }

                for (int i = 0; i < read; i++)
                    _InputQueue.Enqueue(buffer[i]);
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_Disposed)
                return;

            _Disposed = true;
            Stop();
            _OutputStream?.Dispose();
            _InputStream?.Dispose();
        }
    }
}

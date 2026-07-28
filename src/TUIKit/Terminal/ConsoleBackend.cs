namespace TUIKit.Terminal
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;

    /// <summary>
    /// An <see cref="ITerminalBackend"/> backed by the real process console. On Windows it enables
    /// virtual terminal processing and raw input through the console API; on Unix-like systems it
    /// puts the terminal into raw mode with <c>stty</c>. A background thread reads raw input bytes so
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

            if (!_Interactive)
                return;

            if (_IsWindows)
                EnableWindowsRawMode();
            else
                RunStty("raw -echo");

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
            if (_OutputStream == null)
                return;

            string pending;
            lock (_WriteSync)
            {
                if (_WriteBuffer.Length == 0)
                    return;

                pending = _WriteBuffer.ToString();
                _WriteBuffer.Clear();
            }

            byte[] bytes = Encoding.UTF8.GetBytes(pending);
            _OutputStream.Write(bytes, 0, bytes.Length);
            _OutputStream.Flush();
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
                if (_IsWindows)
                    RestoreWindowsMode();
                else
                    RunStty("sane");
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

        private static void RunStty(string arguments)
        {
            try
            {
                ProcessStartInfo info = new ProcessStartInfo
                {
                    FileName = "/bin/sh",
                    Arguments = "-c \"stty " + arguments + " </dev/tty\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false
                };

                using (Process? process = Process.Start(info))
                {
                    process?.WaitForExit(2000);
                }
            }
            catch (Exception)
            {
                // Best effort: if stty is unavailable the terminal simply stays in cooked mode.
            }
        }

        private void ReaderLoop()
        {
            if (_InputStream == null)
                return;

            byte[] buffer = new byte[1024];
            while (_Running)
            {
                int read;
                try
                {
                    read = _InputStream.Read(buffer, 0, buffer.Length);
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

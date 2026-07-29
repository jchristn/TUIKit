namespace TUIKit.Terminal
{
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Unix terminal interop that places the controlling terminal into raw mode in-process via the
    /// libc <c>termios</c> API (<c>tcgetattr</c>, <c>cfmakeraw</c>, <c>tcsetattr</c>). Raw mode
    /// disables canonical line buffering, echo, and signal generation so that individual keystrokes,
    /// arrow keys, function keys, and control sequences reach the application unmodified — matching the
    /// behavior the Windows console API provides. Methods are only meaningful on Unix-like systems;
    /// callers guard by operating system. All members are thread-safe.
    /// </summary>
    /// <remarks>
    /// The terminal attributes are treated as an opaque byte blob: libc's <c>cfmakeraw</c> sets the
    /// correct per-platform flags, so no knowledge of the <c>struct termios</c> layout (which differs
    /// between Linux and macOS) is required. On non-Unix platforms the P/Invoke targets are never
    /// called. <c>libc</c> resolves to <c>libSystem.dylib</c> on macOS and <c>libc.so</c> on Linux.
    /// </remarks>
    internal static class PosixTerminal
    {
        // The controlling terminal's standard input and output file descriptors.
        private const int StdInputFileDescriptor = 0;
        private const int StdOutputFileDescriptor = 1;

        // errno EINTR: a call was interrupted by a signal and should be retried.
        private const int Eintr = 4;

        // tcsetattr: apply the change immediately without discarding pending input (TCSANOW == 0 on
        // both Linux and macOS).
        private const int SetAttributesNow = 0;

        // Generous fixed buffer for struct termios. The real struct is ~60 bytes on Linux and ~72 on
        // macOS; 128 leaves ample headroom and is only ever read/written by libc within its true size.
        private const int TermiosBufferSize = 128;

        [DllImport("libc", SetLastError = true)]
        private static extern int tcgetattr(int fd, byte[] termios);

        [DllImport("libc", SetLastError = true)]
        private static extern int tcsetattr(int fd, int optionalActions, byte[] termios);

        [DllImport("libc")]
        private static extern void cfmakeraw(byte[] termios);

        [DllImport("libc", SetLastError = true, EntryPoint = "read")]
        private static extern IntPtr ReadNative(int fd, byte[] buffer, UIntPtr count);

        [DllImport("libc", SetLastError = true, EntryPoint = "write")]
        private static extern IntPtr WriteNative(int fd, IntPtr buffer, UIntPtr count);

        /// <summary>
        /// Reads up to <paramref name="count"/> bytes of raw input directly from standard input,
        /// bypassing <see cref="Console"/> (whose Unix implementation echoes and re-cooks the
        /// terminal). Blocks until at least one byte is available while in raw mode.
        /// </summary>
        /// <param name="buffer">The destination buffer. Must not be null.</param>
        /// <param name="count">The maximum number of bytes to read.</param>
        /// <returns>The number of bytes read, 0 at end of input, or -1 on error.</returns>
        internal static int ReadStdin(byte[] buffer, int count)
        {
            while (true)
            {
                IntPtr result = ReadNative(StdInputFileDescriptor, buffer, (UIntPtr)count);
                int read = (int)result;
                if (read < 0 && Marshal.GetLastWin32Error() == Eintr)
                    continue;

                return read;
            }
        }

        /// <summary>
        /// Writes <paramref name="length"/> bytes of <paramref name="data"/> to standard output
        /// directly, bypassing <see cref="Console"/>. Retries short writes and signal interruptions.
        /// </summary>
        /// <param name="data">The bytes to write. Must not be null.</param>
        /// <param name="length">The number of bytes at the start of <paramref name="data"/> to write.</param>
        internal static void WriteStdout(byte[] data, int length)
        {
            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                IntPtr basePointer = handle.AddrOfPinnedObject();
                int offset = 0;
                while (offset < length)
                {
                    IntPtr result = WriteNative(StdOutputFileDescriptor, IntPtr.Add(basePointer, offset), (UIntPtr)(length - offset));
                    int written = (int)result;
                    if (written < 0)
                    {
                        if (Marshal.GetLastWin32Error() == Eintr)
                            continue;

                        break;
                    }

                    if (written == 0)
                        break;

                    offset += written;
                }
            }
            finally
            {
                handle.Free();
            }
        }

        /// <summary>
        /// Reads the current terminal attributes and switches standard input to raw mode.
        /// </summary>
        /// <returns>
        /// The saved original attributes to pass to <see cref="RestoreMode"/>, or null when the
        /// attributes could not be read (for example when standard input is not a terminal). A null
        /// result means the terminal was left unchanged.
        /// </returns>
        internal static byte[]? EnterRawMode()
        {
            byte[] original = new byte[TermiosBufferSize];

            if (tcgetattr(StdInputFileDescriptor, original) != 0)
                return null;

            byte[] raw = new byte[TermiosBufferSize];
            Array.Copy(original, raw, TermiosBufferSize);

            cfmakeraw(raw);

            if (tcsetattr(StdInputFileDescriptor, SetAttributesNow, raw) != 0)
                return null;

            return original;
        }

        /// <summary>
        /// Restores previously saved terminal attributes.
        /// </summary>
        /// <param name="saved">
        /// The attributes returned by <see cref="EnterRawMode"/>. When null the call is a no-op, so it
        /// is safe to invoke even when raw mode was never entered.
        /// </param>
        internal static void RestoreMode(byte[]? saved)
        {
            if (saved == null)
                return;

            tcsetattr(StdInputFileDescriptor, SetAttributesNow, saved);
        }
    }
}

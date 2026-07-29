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
        // The controlling terminal's standard input file descriptor.
        private const int StdInputFileDescriptor = 0;

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

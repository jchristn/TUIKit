namespace TUIKit.Terminal
{
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Windows console interop used to enable virtual terminal processing and raw input. Methods are
    /// no-ops or return false on non-Windows platforms; callers guard by operating system.
    /// </summary>
    internal static class NativeConsole
    {
        internal const int StdInputHandle = -10;
        internal const int StdOutputHandle = -11;

        internal const uint EnableProcessedInput = 0x0001;
        internal const uint EnableLineInput = 0x0002;
        internal const uint EnableEchoInput = 0x0004;
        internal const uint EnableVirtualTerminalInput = 0x0200;
        internal const uint EnableVirtualTerminalProcessing = 0x0004;

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);
    }
}

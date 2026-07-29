namespace TUIKit.Hosting
{
#if !NET8_0_OR_GREATER
    using System;
    using System.Runtime.InteropServices;
    using TUIKit;

    /// <summary>
    /// A <c>netstandard2.0</c> compatibility shim that hooks POSIX signals through libc
    /// <c>signal()</c>, standing in for <c>PosixSignalRegistration</c> (which is unavailable on that
    /// target). It routes SIGINT, SIGWINCH, SIGTSTP, and SIGCONT to an <see cref="AppLifecycle"/>.
    /// The managed handler is invoked from a native signal context, which is not async-signal-safe, so
    /// this is a best-effort fallback; on .NET 8 or later the safer registration API is used instead.
    /// Handlers are process-global, so a single lifecycle should own the shim.
    /// </summary>
    internal sealed class PosixSignalShim : IDisposable
    {
        private delegate void SignalHandler(int signal);

        private const int SigInt = 2;
        private const int SigWinch = 28;

        private readonly AppLifecycle _Owner;
        private readonly SignalHandler _Handler;
        private readonly int _Tstp;
        private readonly int _Cont;
        private bool _Disposed;

        private PosixSignalShim(AppLifecycle owner)
        {
            _Owner = owner;
            _Handler = OnSignal;

            bool isMac = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
            _Tstp = isMac ? 18 : 20;
            _Cont = isMac ? 19 : 18;
        }

        [DllImport("libc", EntryPoint = "signal", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SignalDelegate(int signum, SignalHandler handler);

        [DllImport("libc", EntryPoint = "signal", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SignalDefault(int signum, IntPtr handler);

        internal static IDisposable Hook(AppLifecycle owner)
        {
            PosixSignalShim shim = new PosixSignalShim(owner);
            SignalDelegate(SigInt, shim._Handler);
            SignalDelegate(SigWinch, shim._Handler);
            SignalDelegate(shim._Tstp, shim._Handler);
            SignalDelegate(shim._Cont, shim._Handler);
            return shim;
        }

        public void Dispose()
        {
            if (_Disposed)
                return;

            _Disposed = true;
            IntPtr sigDfl = IntPtr.Zero; // SIG_DFL restores the default disposition.
            SignalDefault(SigInt, sigDfl);
            SignalDefault(SigWinch, sigDfl);
            SignalDefault(_Tstp, sigDfl);
            SignalDefault(_Cont, sigDfl);
        }

        private void OnSignal(int signum)
        {
            if (signum == SigInt)
                _Owner.RaiseInterrupted();
            else if (signum == SigWinch)
                _Owner.RaiseResized(new Size(0, 0));
            else if (signum == _Tstp)
                _Owner.RaiseSuspending();
            else if (signum == _Cont)
                _Owner.RaiseResumed();
        }
    }
#endif
}

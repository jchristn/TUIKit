namespace TUIKit.Hosting
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
#if NET8_0_OR_GREATER
    using System.Runtime.InteropServices;
#endif

    /// <summary>
    /// Surfaces terminal lifecycle events — suspend (Ctrl+Z / SIGTSTP), resume (SIGCONT), resize
    /// (SIGWINCH), and interrupt (SIGINT) — as ordinary .NET events so an app can restore raw mode and
    /// redraw at the right moments. On .NET 8+ Unix, <see cref="HookPosixSignals"/> wires real signals;
    /// on other targets it is a no-op. The Raise* methods let a host (or a test) drive the events
    /// directly, keeping the behavior deterministic and portable.
    /// </summary>
    public sealed class AppLifecycle : IDisposable
    {
        private readonly List<IDisposable> _Registrations = new List<IDisposable>();
        private bool _Disposed;

        /// <summary>Raised when the process is about to be suspended. Restore the terminal here.</summary>
        public event Action? Suspending;

        /// <summary>Raised when the process resumes from suspension. Re-enter raw mode and redraw here.</summary>
        public event Action? Resumed;

        /// <summary>Raised when the terminal is resized, with the new size.</summary>
        public event Action<Size>? Resized;

        /// <summary>Raised on an interrupt request (SIGINT / Ctrl+C at the OS level).</summary>
        public event Action? Interrupted;

        /// <summary>Gets whether OS signal handlers are currently hooked.</summary>
        public bool SignalsHooked
        {
            get { return _Registrations.Count > 0; }
        }

        /// <summary>Raises <see cref="Suspending"/>.</summary>
        public void RaiseSuspending()
        {
            Suspending?.Invoke();
        }

        /// <summary>Raises <see cref="Resumed"/>.</summary>
        public void RaiseResumed()
        {
            Resumed?.Invoke();
        }

        /// <summary>Raises <see cref="Resized"/> with the given size.</summary>
        /// <param name="size">The new terminal size.</param>
        public void RaiseResized(Size size)
        {
            Resized?.Invoke(size);
        }

        /// <summary>Raises <see cref="Interrupted"/>.</summary>
        public void RaiseInterrupted()
        {
            Interrupted?.Invoke();
        }

        /// <summary>
        /// Hooks OS signal handlers where supported (.NET 8+ on Unix). Safe to call on any platform;
        /// it does nothing when signals are unavailable. Calling more than once is a no-op after the
        /// first success.
        /// </summary>
        public void HookPosixSignals()
        {
            if (_Registrations.Count > 0)
                return;

#if NET8_0_OR_GREATER
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return;

            _Registrations.Add(PosixSignalRegistration.Create(PosixSignal.SIGTSTP, _ => RaiseSuspending()));
            _Registrations.Add(PosixSignalRegistration.Create(PosixSignal.SIGCONT, _ => RaiseResumed()));
            _Registrations.Add(PosixSignalRegistration.Create(PosixSignal.SIGINT, context =>
            {
                context.Cancel = true;
                RaiseInterrupted();
            }));

            PosixSignal winch = (PosixSignal)28; // SIGWINCH has no named enum member.
            try
            {
                _Registrations.Add(PosixSignalRegistration.Create(winch, _ => RaiseResized(new Size(0, 0))));
            }
            catch (ArgumentException)
            {
                // SIGWINCH not supported on this platform; ignore.
            }
#endif
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_Disposed)
                return;

            _Disposed = true;
            for (int i = 0; i < _Registrations.Count; i++)
                _Registrations[i].Dispose();

            _Registrations.Clear();
        }
    }
}

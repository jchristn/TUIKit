namespace TUIKit.Terminal
{
    /// <summary>
    /// Optional companion to <see cref="ITerminalBackend"/> for a backend that may not own a real
    /// terminal. <c>TuiApplication.Start</c> allows only one running application per process because a
    /// terminal is a singleton resource; a backend that reports <see cref="ClaimsTerminal"/> false is
    /// exempt, so any number of such applications can run side by side (for example parallel UI tests
    /// on <see cref="HeadlessBackend"/>).
    /// </summary>
    /// <remarks>
    /// A backend that does not implement this interface is treated as claiming the terminal, so
    /// <see cref="ITerminalBackend"/> gains no member and third-party backends keep their 1.4.0 behavior.
    /// An application whose backend does not claim the terminal also skips the process-wide parts of a
    /// session: it installs no Ctrl+C or process-exit handler, does not count in the
    /// <c>tuikit.sessions.active</c> counter or record a session duration, and does not overwrite the
    /// session-shape gauges (target FPS, columns, rows) that describe the real terminal session. The
    /// value is read once, when the application starts. Implementations must be thread-safe.
    /// </remarks>
    public interface ISharedTerminalBackend
    {
        /// <summary>
        /// Gets a value indicating whether an application started on this backend takes the process's
        /// one terminal slot. False lets other applications start alongside it.
        /// </summary>
        bool ClaimsTerminal { get; }
    }
}

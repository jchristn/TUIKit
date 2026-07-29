namespace TUIKit.Hosting
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using TUIKit.Terminal;

    /// <summary>
    /// One-call entry point for a terminal application. It creates a <see cref="ConsoleBackend"/> and a
    /// <see cref="TuiApplication"/> with sensible defaults (Ctrl+C quits), invokes the configuration
    /// callback so the caller can add panes, widgets, and keybindings, and then runs the loop, disposing
    /// everything on exit. Use <see cref="TuiApplication"/> directly when you need a custom backend.
    /// </summary>
    public static class TuiApp
    {
        /// <summary>
        /// Builds a console application, runs it, and disposes it.
        /// </summary>
        /// <param name="configure">A callback that configures the application. Must not be null.</param>
        /// <param name="cancellationToken">A token used to stop the loop.</param>
        /// <returns>A task that completes when the loop exits.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is null.</exception>
        public static async Task RunAsync(Action<TuiApplication> configure, CancellationToken cancellationToken = default)
        {
            if (configure == null)
                throw new ArgumentNullException(nameof(configure));

            using (ConsoleBackend backend = new ConsoleBackend())
            using (TuiApplication app = new TuiApplication(backend))
            {
                configure(app);
                await app.RunAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}

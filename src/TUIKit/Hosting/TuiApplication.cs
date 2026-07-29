namespace TUIKit.Hosting
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Input;
    using TUIKit.Layout;
    using TUIKit.Modals;
    using TUIKit.Rendering;
    using TUIKit.Terminal;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    /// <summary>
    /// Hosts a TUIKit session over a terminal backend: it owns the render loop, the input loop, the
    /// region layout and the panes bound to it, the command router, the modal stack, and the
    /// notification center. The terminal is a singleton resource, so at most one application may run
    /// at a time. When the backend is not interactive the host degrades to plain line output.
    /// </summary>
    /// <remarks>
    /// Rendering happens on the render thread. Panes are thread-safe, so any thread may write to them
    /// while the loop runs. <see cref="PumpInputOnce"/> and <see cref="RenderOnce"/> are exposed so a
    /// test can drive a headless backend deterministically without starting the loop.
    /// </remarks>
    public sealed class TuiApplication : IDisposable
    {
        private static int _ActiveCount;

        private readonly ITerminalBackend _Backend;
        private readonly Dictionary<string, IWidget> _Content = new Dictionary<string, IWidget>(StringComparer.Ordinal);
        private readonly Dictionary<string, Action> _Commands = new Dictionary<string, Action>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _LineModeEmitted = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly CommandRoutingTable _Routing = new CommandRoutingTable();
        private readonly CommandRouter _Router;
        private readonly ModalStack _Modals = new ModalStack();
        private readonly NotificationCenter _Notifications = new NotificationCenter();
        private readonly InputParser _Parser = new InputParser();
        private readonly Stopwatch _Clock = new Stopwatch();
        private readonly byte[] _ReadBuffer = new byte[4096];

        private TerminalRenderer? _Renderer;
        private Layout? _Layout;
        private Theme _Theme = Theme.Dark;
        private string? _FocusContext;
        private CtrlCPolicy _CtrlCPolicy = CtrlCPolicy.Kill;
        private Action<ISurface>? _OnRenderOverlay;
        private bool _AutoRenderNotifications = true;
        private int _TargetFps = 60;
        private volatile bool _Running;
        private volatile bool _StopRequested;
        private long _LastCtrlC = long.MinValue;
        private bool _Started;
        private bool _Disposed;
        private bool _MouseCaptureEnabled = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="TuiApplication"/> class.
        /// </summary>
        /// <param name="backend">The terminal backend. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="backend"/> is null.</exception>
        public TuiApplication(ITerminalBackend backend)
        {
            _Backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _Router = new CommandRouter(_Routing);
        }

        /// <summary>
        /// Gets the command routing table.
        /// </summary>
        public CommandRoutingTable Commands
        {
            get { return _Routing; }
        }

        /// <summary>
        /// Gets the modal stack.
        /// </summary>
        public ModalStack Modals
        {
            get { return _Modals; }
        }

        /// <summary>
        /// Gets the notification center.
        /// </summary>
        public NotificationCenter Notifications
        {
            get { return _Notifications; }
        }

        /// <summary>
        /// Gets or sets the active theme. Defaults to <see cref="Theme.Dark"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public Theme Theme
        {
            get { return _Theme; }
            set
            {
                _Theme = value ?? throw new ArgumentNullException(nameof(value));
                _Renderer?.Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the region layout. Must be set before panes are useful.
        /// </summary>
        public Layout? Layout
        {
            get { return _Layout; }
            set { _Layout = value; }
        }

        /// <summary>
        /// Gets or sets the current focus context used for focus-scoped key bindings.
        /// </summary>
        public string? FocusContext
        {
            get { return _FocusContext; }
            set { _FocusContext = value; }
        }

        /// <summary>
        /// Gets or sets how Ctrl+C is handled. Defaults to <see cref="CtrlCPolicy.Kill"/>.
        /// </summary>
        public CtrlCPolicy CtrlCPolicy
        {
            get { return _CtrlCPolicy; }
            set { _CtrlCPolicy = value; }
        }

        /// <summary>
        /// Gets or sets whether the application captures mouse input. Defaults to <c>true</c>. While
        /// capture is on, the terminal's own click-drag selection is suppressed (mouse events flow to
        /// the app instead). Turn it off to hand the mouse back to the terminal so the user can select
        /// and copy text natively — for example to paste into another program — then turn it back on.
        /// Setting this after <see cref="Start"/> emits the enable/disable escape immediately; it is a
        /// no-op on a non-interactive backend. Thread-safe with respect to the render loop only in that
        /// the escape is written directly to the backend.
        /// </summary>
        public bool MouseCaptureEnabled
        {
            get { return _MouseCaptureEnabled; }
            set
            {
                if (_MouseCaptureEnabled == value)
                    return;

                _MouseCaptureEnabled = value;
                if (_Started && _Backend.IsInteractive)
                {
                    _Backend.Write(value ? Ansi.EnableMouse : Ansi.DisableMouse);
                    _Backend.Flush();
                }
            }
        }

        /// <summary>
        /// Toggles <see cref="MouseCaptureEnabled"/> and returns the new state. Bind this to a key
        /// (F12 in the sample app) to let the user switch between interacting with widgets and using
        /// the terminal's native text selection.
        /// </summary>
        /// <returns>The new value of <see cref="MouseCaptureEnabled"/>.</returns>
        public bool ToggleMouseCapture()
        {
            MouseCaptureEnabled = !_MouseCaptureEnabled;
            return _MouseCaptureEnabled;
        }

        /// <summary>
        /// Gets or sets a callback invoked after panes are rendered so the host application can draw
        /// overlays (status bars, hints, link decorations).
        /// </summary>
        public Action<ISurface>? RenderOverlay
        {
            get { return _OnRenderOverlay; }
            set { _OnRenderOverlay = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the host renders the notification center itself in
        /// the top-right corner after the overlay. Set to false to position toasts yourself from the
        /// <see cref="RenderOverlay"/> callback (for example to keep them clear of a fixed side panel).
        /// Defaults to true.
        /// </summary>
        public bool AutoRenderNotifications
        {
            get { return _AutoRenderNotifications; }
            set { _AutoRenderNotifications = value; }
        }

        /// <summary>
        /// Gets or sets the target frame rate used to coalesce repaints. Defaults to 60. Must be
        /// between 1 and 240.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when out of range.</exception>
        public int TargetFps
        {
            get { return _TargetFps; }
            set
            {
                if (value < 1 || value > 240)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Target FPS must be between 1 and 240.");
                _TargetFps = value;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the run loop is active.
        /// </summary>
        public bool IsRunning
        {
            get { return _Running; }
        }

        /// <summary>
        /// Gets the milliseconds elapsed since the application started, used for notification timing.
        /// </summary>
        public long NowMilliseconds
        {
            get { return _Clock.ElapsedMilliseconds; }
        }

        /// <summary>
        /// Raised for a key that was not consumed by a modal or a command binding, so the application
        /// can route it to text input.
        /// </summary>
        public event Action<KeyEvent>? KeyReceived;

        /// <summary>
        /// Raised for a bracketed-paste event.
        /// </summary>
        public event Action<string>? PasteReceived;

        /// <summary>
        /// Raised for a mouse event.
        /// </summary>
        public event Action<MouseEvent>? MouseReceived;

        /// <summary>
        /// Raised when Ctrl+C is pressed under the <see cref="CtrlCPolicy.InterruptFocusedPane"/> policy.
        /// </summary>
        public event Action? Interrupted;

        /// <summary>
        /// Binds a pane to a layout region so the host renders it there.
        /// </summary>
        /// <param name="regionId">The region identifier. Must not be null or empty.</param>
        /// <param name="pane">The pane. Must not be null.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="regionId"/> is null or empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="pane"/> is null.</exception>
        public void BindPane(string regionId, Pane pane)
        {
            Bind(regionId, pane);
        }

        /// <summary>
        /// Binds any widget to a layout region so the host measures, arranges, and renders it there.
        /// Panes are rendered with the theme background; other widgets render over a themed fill.
        /// </summary>
        /// <param name="regionId">The region identifier. Must not be null or empty.</param>
        /// <param name="widget">The widget. Must not be null.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="regionId"/> is null or empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="widget"/> is null.</exception>
        public void Bind(string regionId, IWidget widget)
        {
            if (string.IsNullOrEmpty(regionId))
                throw new ArgumentException("Region id must not be null or empty.", nameof(regionId));
            if (widget == null)
                throw new ArgumentNullException(nameof(widget));

            _Content[regionId] = widget;
        }

        /// <summary>
        /// Appends a region to the layout.
        /// </summary>
        /// <param name="id">The region identifier. Must not be null or empty.</param>
        /// <param name="configure">A callback that configures the region. Must not be null.</param>
        /// <returns>The created region.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is null.</exception>
        public Region AddRegion(string id, Action<RegionBuilder> configure)
        {
            if (configure == null)
                throw new ArgumentNullException(nameof(configure));

            RegionBuilder builder = new RegionBuilder(id);
            configure(builder);
            Region region = builder.Build();

            List<Region> regions = _Layout != null ? new List<Region>(_Layout.Regions) : new List<Region>();
            regions.Add(region);
            _Layout = new Layout(regions);
            _Renderer?.Invalidate();
            return region;
        }

        /// <summary>
        /// Creates a pane, adds a region for it, binds it, and returns the pane. When no region
        /// configuration is supplied the region fills the whole surface.
        /// </summary>
        /// <param name="id">The pane and region identifier. Must not be null or empty.</param>
        /// <param name="configure">An optional region configuration; defaults to filling the surface.</param>
        /// <returns>The created pane.</returns>
        public Pane AddPane(string id, Action<RegionBuilder>? configure = null)
        {
            AddRegion(id, configure ?? (r => r.FillWidth().FillHeight()));
            Pane pane = new Pane(id);
            Bind(id, pane);
            return pane;
        }

        /// <summary>
        /// Adds a region for a widget, binds the widget, and returns it. When no region configuration
        /// is supplied the region fills the whole surface.
        /// </summary>
        /// <typeparam name="TWidget">The widget type.</typeparam>
        /// <param name="id">The region identifier. Must not be null or empty.</param>
        /// <param name="widget">The widget. Must not be null.</param>
        /// <param name="configure">An optional region configuration; defaults to filling the surface.</param>
        /// <returns>The widget.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="widget"/> is null.</exception>
        public TWidget AddWidget<TWidget>(string id, TWidget widget, Action<RegionBuilder>? configure = null)
            where TWidget : IWidget
        {
            if (widget == null)
                throw new ArgumentNullException(nameof(widget));

            AddRegion(id, configure ?? (r => r.FillWidth().FillHeight()));
            Bind(id, widget);
            return widget;
        }

        /// <summary>
        /// Registers a command handler invoked when the command's chord fires.
        /// </summary>
        /// <param name="commandId">The command identifier. Must not be null or empty.</param>
        /// <param name="handler">The handler. Must not be null.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="commandId"/> is null or empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is null.</exception>
        public void RegisterCommand(string commandId, Action handler)
        {
            if (string.IsNullOrEmpty(commandId))
                throw new ArgumentException("Command id must not be null or empty.", nameof(commandId));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            _Commands[commandId] = handler;
        }

        /// <summary>
        /// Binds a key chord directly to an action, replacing any existing binding for that chord.
        /// A convenience over <see cref="RegisterCommand"/> plus a routing-table registration for the
        /// common case where a config-file command id is not needed.
        /// </summary>
        /// <param name="chord">The chord string, such as <c>"ctrl+q"</c>. Must not be null or empty.</param>
        /// <param name="action">The action to run. Must not be null.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="chord"/> is null or empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
        public void Bind(string chord, Action action)
        {
            if (string.IsNullOrEmpty(chord))
                throw new ArgumentException("Chord must not be null or empty.", nameof(chord));
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            KeyChord parsed = KeyChord.Parse(chord);
            string id = "bind:" + parsed;
            _Routing.Unregister(parsed);
            _Commands[id] = action;
            _Routing.Register(parsed, id);
        }

        /// <summary>
        /// Requests the run loop to exit. Suitable as a method group for <see cref="Bind(string, Action)"/>.
        /// </summary>
        public void Quit()
        {
            RequestStop();
        }

        /// <summary>
        /// Shows a modal and returns its result. The input loop drives it to completion.
        /// </summary>
        /// <param name="modal">The modal. Must not be null.</param>
        /// <returns>A task that completes with the modal's result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="modal"/> is null.</exception>
        public Task<object?> ShowAsync(Modal modal)
        {
            if (modal == null)
                throw new ArgumentNullException(nameof(modal));

            _Modals.Push(modal);
            return modal.Completion;
        }

        /// <summary>
        /// Shows a confirmation dialog and returns whether the confirm button was chosen.
        /// </summary>
        /// <param name="message">The message. Must not be null.</param>
        /// <param name="confirmLabel">The confirm button label. Defaults to "Yes".</param>
        /// <param name="cancelLabel">The cancel button label. Defaults to "No".</param>
        /// <returns><c>true</c> when the confirm button was chosen; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is null.</exception>
        public async Task<bool> ConfirmAsync(string message, string confirmLabel = "Yes", string cancelLabel = "No")
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            MessageModal modal = new MessageModal("Confirm", message, new[] { confirmLabel, cancelLabel });
            object? result = await ShowAsync(modal).ConfigureAwait(false);
            return result is int index && index == 0;
        }

        /// <summary>
        /// Shows a text-input dialog and returns the entered value, or null when cancelled.
        /// </summary>
        /// <param name="title">The prompt title. Must not be null.</param>
        /// <param name="initial">The initial value. Defaults to empty.</param>
        /// <returns>The entered text, or null when cancelled.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="title"/> is null.</exception>
        public async Task<string?> PromptAsync(string title, string initial = "")
        {
            if (title == null)
                throw new ArgumentNullException(nameof(title));

            PromptModal modal = new PromptModal(title, initial);
            object? result = await ShowAsync(modal).ConfigureAwait(false);
            return result as string;
        }

        /// <summary>
        /// Shows a selection dialog and returns the chosen zero-based index, or -1 when cancelled.
        /// </summary>
        /// <param name="title">The title. Must not be null.</param>
        /// <param name="options">The options. Must not be null or empty.</param>
        /// <returns>The chosen index, or -1 when cancelled.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="title"/> or <paramref name="options"/> is null.</exception>
        public async Task<int> SelectAsync(string title, params string[] options)
        {
            if (title == null)
                throw new ArgumentNullException(nameof(title));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            SelectModal modal = new SelectModal(title, options);
            object? result = await ShowAsync(modal).ConfigureAwait(false);
            return result is int index ? index : -1;
        }

        /// <summary>
        /// Raises a transient notification (toast).
        /// </summary>
        /// <param name="text">The message. Must not be null.</param>
        /// <param name="severity">The severity, which drives the color. Defaults to informational.</param>
        /// <param name="timeoutMilliseconds">The timeout, or null for the notification center default.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public void Notify(string text, NotificationSeverity severity = NotificationSeverity.Info, int? timeoutMilliseconds = null)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            _Notifications.Add(text, severity, NowMilliseconds, timeoutMilliseconds);
        }

        /// <summary>
        /// Starts the terminal session: enters raw mode and, when interactive, the alternate screen
        /// with mouse, paste, and enhanced keyboard enabled.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when another application is already running.</exception>
        public void Start()
        {
            if (_Started)
                return;

            if (Interlocked.Increment(ref _ActiveCount) != 1)
            {
                Interlocked.Decrement(ref _ActiveCount);
                throw new InvalidOperationException("The terminal is a singleton resource; only one TuiApplication may run at a time.");
            }

            _Started = true;
            _Clock.Start();
            _Backend.Start();

            _Renderer = new TerminalRenderer(
                Math.Max(1, _Backend.Size.Width),
                Math.Max(1, _Backend.Size.Height),
                _Backend.Capabilities.ColorDepth);

            if (_Backend.IsInteractive)
            {
                _Backend.Write(Ansi.EnterAltScreen);
                _Backend.Write(Ansi.HideCursor);
                if (_MouseCaptureEnabled)
                    _Backend.Write(Ansi.EnableMouse);
                _Backend.Write(Ansi.EnableBracketedPaste);
                if (_Backend.Capabilities.EnhancedKeyboard)
                    _Backend.Write(Ansi.PushKittyKeyboard);
                _Backend.Flush();
            }
        }

        /// <summary>
        /// Stops the session and restores the terminal to its original state.
        /// </summary>
        public void Stop()
        {
            if (!_Started)
                return;

            _Running = false;

            if (_Backend.IsInteractive)
            {
                if (_Backend.Capabilities.EnhancedKeyboard)
                    _Backend.Write(Ansi.PopKittyKeyboard);
                _Backend.Write(Ansi.DisableBracketedPaste);
                _Backend.Write(Ansi.DisableMouse);
                _Backend.Write(Ansi.ShowCursor);
                _Backend.Write(Ansi.ExitAltScreen);
                _Backend.Flush();
            }

            _Backend.Stop();
            _Started = false;
            Interlocked.Decrement(ref _ActiveCount);
        }

        /// <summary>
        /// Requests that the run loop exit at the next opportunity.
        /// </summary>
        public void RequestStop()
        {
            _StopRequested = true;
        }

        /// <summary>
        /// Runs the input and render loop until a stop is requested or the token is cancelled.
        /// </summary>
        /// <param name="cancellationToken">A token used to stop the loop.</param>
        /// <returns>A task that completes when the loop exits.</returns>
        public async Task RunAsync(CancellationToken cancellationToken)
        {
            if (!_Started)
                Start();

            _Running = true;
            _StopRequested = false;
            int frameDelay = Math.Max(1, 1000 / _TargetFps);

            try
            {
                while (!_StopRequested && !cancellationToken.IsCancellationRequested)
                {
                    PumpInputOnce();
                    RenderOnce();
                    await Task.Delay(frameDelay, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation.
            }
            finally
            {
                _Running = false;
            }
        }

        /// <summary>
        /// Reads and dispatches any pending input once. Safe to call from a driving test.
        /// </summary>
        public void PumpInputOnce()
        {
            int read = _Backend.ReadInput(_ReadBuffer, 0, _ReadBuffer.Length);
            if (read > 0)
            {
                _Parser.Feed(_ReadBuffer, read);
                foreach (InputEvent inputEvent in _Parser.Drain())
                    Dispatch(inputEvent);
            }
            else
            {
                foreach (InputEvent inputEvent in _Parser.Flush())
                    Dispatch(inputEvent);
            }
        }

        /// <summary>
        /// Composes and emits one frame. Safe to call from a driving test.
        /// </summary>
        public void RenderOnce()
        {
            if (_Renderer == null)
                return;

            if (!_Backend.IsInteractive)
            {
                RenderLineMode();
                return;
            }

            _Renderer.Render(_Backend, Compose);
        }

        private void Compose(ISurface root)
        {
            Size size = root.Size;

            if (_Layout != null && !_Layout.FitsIn(size))
            {
                LayoutBlockScreen.Render(root, _Layout.MinimumSize, size, _Theme.Text);
                return;
            }

            root.Fill(new Rect(0, 0, size.Width, size.Height), Cell.Blank(_Theme.Text));

            if (_Layout != null)
            {
                BufferSurface? bufferSurface = root as BufferSurface;
                for (int i = 0; i < _Layout.Regions.Count; i++)
                {
                    Region region = _Layout.Regions[i];

                    if (region.HasBorder)
                    {
                        Rect frame = region.Resolve(size).Intersect(new Rect(0, 0, size.Width, size.Height));
                        BorderStyle borderStyle = _Theme.UseAsciiBorders ? BorderStyle.Ascii : region.Border;
                        root.DrawBox(frame, _Theme.Border, borderStyle, region.BorderTitle);
                    }

                    if (!_Content.TryGetValue(region.Id, out IWidget? widget))
                        continue;

                    Rect rect = region.ContentRect(size).Intersect(new Rect(0, 0, size.Width, size.Height));
                    if (rect.IsEmpty)
                        continue;

                    ISurface view = bufferSurface != null ? bufferSurface.CreateView(rect) : root;
                    if (widget is Pane pane)
                    {
                        pane.Render(view, _Theme.Text);
                    }
                    else
                    {
                        view.Fill(new Rect(0, 0, rect.Width, rect.Height), Cell.Blank(_Theme.Text));
                        widget.Render(view);
                    }
                }
            }

            _OnRenderOverlay?.Invoke(root);

            if (_Modals.IsActive)
                _Modals.Render(root);

            if (_AutoRenderNotifications)
                _Notifications.Render(root, NowMilliseconds);
        }

        private void RenderLineMode()
        {
            foreach (KeyValuePair<string, IWidget> entry in _Content)
            {
                if (!(entry.Value is Pane pane))
                    continue;

                IReadOnlyList<string> lines = pane.SnapshotPlainLines();
                if (!_LineModeEmitted.TryGetValue(entry.Key, out int emitted))
                    emitted = 0;

                for (int i = emitted; i < lines.Count; i++)
                    _Backend.Write(lines[i] + "\n");

                _LineModeEmitted[entry.Key] = lines.Count;
            }

            _Backend.Flush();
        }

        private void Dispatch(InputEvent inputEvent)
        {
            switch (inputEvent.Kind)
            {
                case InputEventKind.Key:
                    DispatchKey(inputEvent.Key);
                    break;
                case InputEventKind.Paste:
                    PasteReceived?.Invoke(inputEvent.PasteText ?? string.Empty);
                    break;
                case InputEventKind.Mouse:
                    if (inputEvent.Mouse != null)
                        MouseReceived?.Invoke(inputEvent.Mouse);
                    break;
                default:
                    break;
            }
        }

        private void DispatchKey(KeyEvent key)
        {
            if (IsCtrlC(key) && _CtrlCPolicy != CtrlCPolicy.Custom)
            {
                HandleCtrlC();
                return;
            }

            if (_Modals.IsActive)
            {
                _Modals.HandleKey(key);
                return;
            }

            CommandResolution resolution = _Router.Process(key, _FocusContext);
            if (resolution.Status == CommandResolutionStatus.Command)
            {
                InvokeCommand(resolution.CommandId);
                return;
            }

            if (resolution.Status == CommandResolutionStatus.Pending)
                return;

            KeyReceived?.Invoke(key);
        }

        private void HandleCtrlC()
        {
            switch (_CtrlCPolicy)
            {
                case CtrlCPolicy.Kill:
                    RequestStop();
                    break;
                case CtrlCPolicy.InterruptFocusedPane:
                    Interrupted?.Invoke();
                    break;
                case CtrlCPolicy.DoubleTapToExit:
                    long now = NowMilliseconds;
                    if (now - _LastCtrlC <= 500)
                        RequestStop();
                    else
                        _Notifications.Add("Press Ctrl+C again to exit", NotificationSeverity.Info, now, 1500);
                    _LastCtrlC = now;
                    break;
                default:
                    break;
            }
        }

        private void InvokeCommand(string? commandId)
        {
            if (commandId == null)
                return;

            if (_Commands.TryGetValue(commandId, out Action? handler))
                handler();
        }

        private static bool IsCtrlC(KeyEvent key)
        {
            return key.Code == KeyCode.Character && key.Rune == 'c' && (key.Modifiers & KeyModifiers.Ctrl) != 0;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_Disposed)
                return;

            _Disposed = true;
            Stop();
        }
    }
}

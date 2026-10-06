namespace TUIKit.Hosting
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Diagnostics;
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
        private readonly ClickSynthesizer _ClickSynthesizer = new ClickSynthesizer();
        private readonly Stopwatch _Clock = new Stopwatch();
        private readonly byte[] _ReadBuffer = new byte[4096];
        private readonly List<string> _FocusOrder = new List<string>();
        private readonly ConcurrentQueue<PostedAction> _PostQueue = new ConcurrentQueue<PostedAction>();
        private readonly List<HitTestEntry> _HitMap = new List<HitTestEntry>();

        private TerminalRenderer? _Renderer;
        private Layout? _Layout;
        private Theme _Theme = Theme.Dark;
        private string? _FocusContext;
        private string? _FocusedRegion;
        private CtrlCPolicy _CtrlCPolicy = CtrlCPolicy.Kill;
        private Action<ISurface>? _OnRenderOverlay;
        private bool _AutoRenderNotifications = true;
        private int _TargetFps = 60;
        private volatile bool _Running;
        private volatile bool _StopRequested;
        private long _LastCtrlC = long.MinValue;
        private long _PendingSinceMs = long.MinValue;
        private int _SequenceTimeoutMs = 800;
        private bool _EnableMouseRouting = true;
        private bool _IncrementalRegions;
        private bool _Started;
        private bool _Disposed;
        private bool _MouseCaptureEnabled = true;
        private bool _ForceFullRepaint;
        private volatile bool _Suspended;
        private MouseTrackingMode _MouseTrackingMode = MouseTrackingMode.AnyMotion;
        private string? _HoverRegionId;
        private IWidget? _HoverWidget;
        private Rect _HoverRect;
        private LinkRegistry? _Links;
        private Link? _HoveredLink;
        private int _TornDown;
        private ConsoleCancelEventHandler? _CancelKeyHandler;
        private EventHandler? _ProcessExitHandler;
        private bool _MouseTextSelectionEnabled;
        private bool _SelActive;
        private bool _SelDragging;
        private bool _SelMoved;
        private Rect _SelRegionRect;
        private int _SelAnchorX;
        private int _SelAnchorY;
        private int _SelFocusX;
        private int _SelFocusY;
        private BufferSurface? _LastRoot;
        private Size _LastComposeSize;
        private long _SessionStartTimestamp;
        private bool _ApplyThemeToWidgets;
        private int _IdleFps;
        private int _IdleAfterMs = 1000;
        private long _LastActivityMs;
        private long _LastRenderMs = long.MinValue;
        private long _LastPaneVersions;
        private volatile bool _RenderRequested = true;
        private int _TooltipDelayMs = 600;
        private bool _ShowTooltips = true;
        private int _PointerX = -1;
        private int _PointerY = -1;
        private long _PointerStillSinceMs;
        private readonly Tooltip _Tooltip = new Tooltip();
        private readonly List<object> _FocusScratch = new List<object>();
        private FocusPath _FocusPath = FocusPath.Empty;
        private bool _HighlightFocusedRegion;
        private FocusFrameOptions _FocusFrameOptions = new FocusFrameOptions();
        private bool _JoinRegionBorders;
        private Size _FrameSize;

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
                if (_ApplyThemeToWidgets)
                    ApplyThemeToAll();
                if (_Modals.Top is DialogModal dialog)
                    dialog.ApplyFocusTheme(_Theme);
                _RenderRequested = true;
                _Renderer?.Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the host pushes <see cref="Theme"/> into themeable
        /// components (<see cref="IThemeable"/>): every bound widget (containers forward to their children),
        /// every pushed modal, the notification center, and the host tooltip, on bind and whenever the
        /// theme changes. Defaults to false, so widgets keep their built-in colors unless the application
        /// opts in. Turning it on applies the current theme immediately. Explicit style assignments made
        /// after a theme is applied stay until the next theme change.
        /// </summary>
        public bool ApplyThemeToWidgets
        {
            get { return _ApplyThemeToWidgets; }
            set
            {
                _ApplyThemeToWidgets = value;
                if (value)
                    ApplyThemeToAll();
                _Renderer?.Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the frame rate used by <see cref="RunAsync"/> while the session is idle (no input,
        /// posted actions, pane writes, or <see cref="RequestRender"/> calls for
        /// <see cref="IdleAfterMilliseconds"/>). Input is still polled at <see cref="TargetFps"/>, so the UI
        /// stays responsive; only composing frames slows down. Zero (the default) disables idle throttling.
        /// Must be between 0 and 240.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when out of range.</exception>
        public int IdleFps
        {
            get { return _IdleFps; }
            set
            {
                if (value < 0 || value > 240)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Idle FPS must be between 0 and 240.");
                _IdleFps = value;
            }
        }

        /// <summary>
        /// Gets or sets how long, in milliseconds, the session must be quiet before <see cref="IdleFps"/>
        /// applies. Defaults to 1000. Must be zero or greater.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when negative.</exception>
        public int IdleAfterMilliseconds
        {
            get { return _IdleAfterMs; }
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Idle delay must be zero or greater.");
                _IdleAfterMs = value;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the session is currently idle-throttled (see <see cref="IdleFps"/>).
        /// </summary>
        public bool IsIdle
        {
            get { return _IdleFps > 0 && NowMilliseconds - _LastActivityMs >= _IdleAfterMs; }
        }

        /// <summary>
        /// Asks the loop to compose the next frame even while idle-throttled, for state changes the host
        /// cannot observe (a widget mutated from a timer, for example). Safe to call from any thread.
        /// </summary>
        public void RequestRender()
        {
            _RenderRequested = true;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the host draws tooltips for widgets implementing
        /// <see cref="ITooltipProvider"/> once the pointer rests over them. Defaults to true; it has no
        /// effect on widgets that do not implement the interface.
        /// </summary>
        public bool ShowTooltips
        {
            get { return _ShowTooltips; }
            set { _ShowTooltips = value; }
        }

        /// <summary>
        /// Gets or sets how long the pointer must rest before a tooltip appears, in milliseconds. Defaults
        /// to 600. Must be zero or greater.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when negative.</exception>
        public int TooltipDelayMilliseconds
        {
            get { return _TooltipDelayMs; }
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Tooltip delay must be zero or greater.");
                _TooltipDelayMs = value;
            }
        }

        /// <summary>
        /// Gets the tooltip renderer the host uses, for styling.
        /// </summary>
        public Tooltip Tooltip
        {
            get { return _Tooltip; }
        }

        /// <summary>
        /// Gets or sets the region layout. Must be set before panes are useful. Choose one construction
        /// path: either assign a layout built with <see cref="TUIKit.Layout.Layout.Create"/> here, or build
        /// it incrementally with <see cref="AddRegion"/>, <see cref="AddPane"/>, and <see cref="AddWidget"/>.
        /// Assigning a layout after regions were added incrementally is rejected rather than silently
        /// discarding those regions.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when a non-null layout is assigned after regions were already added with
        /// <see cref="AddRegion"/>, <see cref="AddPane"/>, or <see cref="AddWidget"/>.
        /// </exception>
        public Layout? Layout
        {
            get { return _Layout; }
            set
            {
                if (value != null && _IncrementalRegions)
                    throw new InvalidOperationException(
                        "The layout was already built incrementally with AddRegion/AddPane/AddWidget; "
                        + "assigning Layout would discard those regions. Use one construction path.");

                _Layout = value;
                _Renderer?.Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the current focus context used for focus-scoped key bindings. When a focusable
        /// widget is bound and the host focus ring drives focus, this is set automatically to the
        /// focused region's id so focus-scoped commands follow focus. Set it yourself only when not
        /// using the host focus ring.
        /// </summary>
        public string? FocusContext
        {
            get { return _FocusContext; }
            set { _FocusContext = value; }
        }

        /// <summary>
        /// Gets the id of the region whose widget currently holds keyboard focus, or null when nothing
        /// is focused. Focusable widgets join the focus ring in bind order; the first one bound is
        /// focused by default.
        /// </summary>
        public string? FocusedRegion
        {
            get { return _FocusedRegion; }
        }

        /// <summary>
        /// Gets where keyboard focus is right now: the focused region and the chain of widgets from that
        /// region's bound widget down to the focused leaf, through every nested container (see
        /// <see cref="FocusPath"/>). Updated after every input event, focus change, and posted action, and
        /// before every frame, so it reflects focus moves made inside containers as well as between
        /// regions. Never null; <see cref="FocusPath.Empty"/> when nothing holds focus. Read on the UI thread.
        /// </summary>
        public FocusPath CurrentFocusPath
        {
            get { return _FocusPath; }
        }

        /// <summary>
        /// Raised on the UI thread when <see cref="CurrentFocusPath"/> changes, whether focus moved between
        /// regions or within a container. The argument is the new path. Use it to update anything that
        /// depends on what holds focus, such as a breadcrumb; a <see cref="StatusBar"/> bound with
        /// <see cref="BindKeyHints"/> updates itself.
        /// </summary>
        public event Action<FocusPath>? FocusPathChanged;

        /// <summary>
        /// Gets or sets a value indicating whether every bordered region shows when it holds focus: its
        /// border is drawn with heavy lines (heavy ASCII when the theme uses ASCII borders) in the
        /// <see cref="Theme.FocusBorderRole"/> style, and its title gains
        /// <see cref="FocusFrameOptions.TitleMarker"/>. Border cells are reserved whether or not a region
        /// is focused, so focus never shifts content. Regions without a border are unaffected. A region
        /// built with <see cref="RegionBuilder.WithFocusedBorder"/> shows focus even when this is off.
        /// Defaults to false, which keeps the plain borders of earlier versions.
        /// </summary>
        public bool HighlightFocusedRegion
        {
            get { return _HighlightFocusedRegion; }
            set
            {
                _HighlightFocusedRegion = value;
                _RenderRequested = true;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether bordered regions that share an edge (overlap by one
        /// cell) join into one connected frame, meeting in tee, corner, and cross glyphs instead of drawing
        /// two separate lines. When on, the host draws every region background first, then every border
        /// (the focused region's frame last, so it stays whole over a shared edge), then every region's
        /// content. Lay regions out so neighbours overlap by one column or row to share it. Defaults to
        /// false, which keeps the per-region drawing order of earlier versions.
        /// </summary>
        public bool JoinRegionBorders
        {
            get { return _JoinRegionBorders; }
            set
            {
                _JoinRegionBorders = value;
                _RenderRequested = true;
            }
        }

        /// <summary>
        /// Gets a region's outer rectangle (including its border) as of the most recent frame, so a
        /// <see cref="RenderOverlay"/> callback or a test can draw or hit-test against a region without
        /// repeating layout math.
        /// </summary>
        /// <param name="regionId">The region id. Must not be null or empty.</param>
        /// <returns>The rectangle in screen cells, or null when there is no such region or no frame has
        /// been rendered yet.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="regionId"/> is null or empty.</exception>
        public Rect? GetRegionBounds(string regionId)
        {
            Region? region = FindRegion(regionId);
            if (region == null || _FrameSize.Width <= 0 || _FrameSize.Height <= 0)
                return null;

            return region.Resolve(_FrameSize).Intersect(new Rect(0, 0, _FrameSize.Width, _FrameSize.Height));
        }

        /// <summary>
        /// Gets a region's content rectangle (inside its border and padding, where its widget renders) as
        /// of the most recent frame.
        /// </summary>
        /// <param name="regionId">The region id. Must not be null or empty.</param>
        /// <returns>The rectangle in screen cells, or null when there is no such region or no frame has
        /// been rendered yet.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="regionId"/> is null or empty.</exception>
        public Rect? GetRegionContentBounds(string regionId)
        {
            Region? region = FindRegion(regionId);
            if (region == null || _FrameSize.Width <= 0 || _FrameSize.Height <= 0)
                return null;

            return region.ContentRect(_FrameSize).Intersect(new Rect(0, 0, _FrameSize.Width, _FrameSize.Height));
        }

        /// <summary>
        /// Gets or sets the options for focused region frames: the focused border style, the title marker,
        /// and the narrow-space fallback (see <see cref="FocusFrameOptions"/>). A region's own
        /// <see cref="Region.FocusedBorder"/> overrides <see cref="FocusFrameOptions.FocusedBorder"/>; its
        /// <see cref="Region.Border"/> is always used while unfocused. Never null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public FocusFrameOptions FocusFrameOptions
        {
            get { return _FocusFrameOptions; }
            set
            {
                _FocusFrameOptions = value ?? throw new ArgumentNullException(nameof(value));
                _RenderRequested = true;
            }
        }

        /// <summary>
        /// Gets the region ids of focusable bound widgets in tab order. Never null.
        /// </summary>
        public IReadOnlyList<string> FocusOrder
        {
            get { return _FocusOrder; }
        }

        /// <summary>
        /// Gets or sets an optional pre-filter consulted for every key before focus-scoped commands, the
        /// focused widget, and global commands. Return <c>true</c> to consume the key and stop further
        /// routing; return <c>false</c> to let routing continue. Modal input and the Ctrl+C policy still
        /// take precedence over the filter. Defaults to null.
        /// </summary>
        public Func<KeyEvent, bool>? KeyFilter { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the host routes mouse events to bound widgets:
        /// click-to-focus for <see cref="IFocusable"/> widgets and wheel/click forwarding to
        /// <see cref="IMouseAware"/> widgets under the pointer, using a per-frame hit-test map. When a
        /// widget consumes an event the raw <see cref="MouseReceived"/> event is not raised for it.
        /// Defaults to true. Turn it off to receive only raw <see cref="MouseReceived"/> events.
        /// </summary>
        public bool EnableMouseRouting
        {
            get { return _EnableMouseRouting; }
            set { _EnableMouseRouting = value; }
        }

        /// <summary>
        /// Gets or sets how long, in milliseconds, a pending multi-key sequence prefix waits for its
        /// second key before it is abandoned so a later key is not swallowed. Defaults to 800. Must be
        /// at least 1.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set to less than 1.</exception>
        public int SequenceTimeoutMilliseconds
        {
            get { return _SequenceTimeoutMs; }
            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Sequence timeout must be at least 1 millisecond.");
                _SequenceTimeoutMs = value;
            }
        }

        /// <summary>
        /// Raised after keyboard focus moves between focusable bound widgets, with the newly focused
        /// region id (or null when focus was cleared). Raised on the loop thread.
        /// </summary>
        public event Action<string?>? FocusChanged;

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
                    if (value)
                    {
                        string enable = BuildMouseEnableSequence();
                        if (enable.Length > 0)
                            _Backend.Write(enable);
                    }
                    else
                    {
                        _Backend.Write(Ansi.DisableMouse);
                    }

                    _Backend.Flush();
                }

                if (!value)
                    ClearHover(KeyModifiers.None);
            }
        }

        /// <summary>
        /// Gets or sets how much pointer traffic is requested from the terminal while
        /// <see cref="MouseCaptureEnabled"/> is on. Defaults to
        /// <see cref="MouseTrackingMode.AnyMotion"/>, which enables hover (Enter/Leave and
        /// buttonless Move events); terminals without any-motion support degrade silently to
        /// drag-only motion. Set to <see cref="MouseTrackingMode.ButtonsAndDrag"/> to cut hover
        /// traffic on high-latency links, or <see cref="MouseTrackingMode.None"/> to stop mouse
        /// reporting entirely. Changing the value after <see cref="Start"/> rewrites the terminal
        /// modes immediately.
        /// </summary>
        public MouseTrackingMode MouseTrackingMode
        {
            get { return _MouseTrackingMode; }
            set
            {
                if (_MouseTrackingMode == value)
                    return;

                _MouseTrackingMode = value;
                if (_Started && _Backend.IsInteractive && _MouseCaptureEnabled)
                {
                    _Backend.Write(Ansi.DisableMouse);
                    string enable = BuildMouseEnableSequence();
                    if (enable.Length > 0)
                        _Backend.Write(enable);
                    _Backend.Flush();
                }

                if (value == MouseTrackingMode.None)
                    ClearHover(KeyModifiers.None);
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
        /// Gets or sets a value indicating whether the built-in mouse text-selection layer is active.
        /// When on, a left click-drag while the mouse is captured selects text within the region where
        /// the drag began (the selection is clamped to that region's rectangle), and Ctrl+C copies the
        /// selection to the clipboard. Selection reads back from the composited cell buffer, so it works
        /// over any bound widget without per-widget cooperation. Defaults to <c>false</c>, so existing
        /// hosts are unaffected. Has no effect while <see cref="MouseCaptureEnabled"/> is <c>false</c>
        /// (the terminal performs its own native selection then). Setting it to <c>false</c> clears any
        /// active selection.
        /// </summary>
        public bool MouseTextSelectionEnabled
        {
            get { return _MouseTextSelectionEnabled; }
            set
            {
                _MouseTextSelectionEnabled = value;
                if (!value)
                    ClearTextSelection();
            }
        }

        /// <summary>
        /// Gets a value indicating whether a non-empty text selection currently exists. Thread-safety:
        /// read on the application loop thread, the same thread that mutates the selection.
        /// </summary>
        public bool HasTextSelection
        {
            get { return _SelActive; }
        }

        /// <summary>
        /// Gets or sets the style used to paint selected cells. When <c>null</c> (the default) selected
        /// cells keep their existing content drawn with reverse video, which reads correctly on any
        /// background. Set a style to override the highlight appearance.
        /// </summary>
        public CellStyle? SelectionStyle { get; set; }

        /// <summary>
        /// Raised on the application loop thread after Ctrl+C copies a selection, carrying the copied
        /// text. Use it for host feedback such as a toast. Never raised with null or empty text.
        /// </summary>
        public event Action<string>? TextCopied;

        /// <summary>
        /// Returns the plain text of the current selection, read from the last composed frame's cell
        /// buffer. Rows are joined with '\n' and trailing spaces are trimmed per row, matching how
        /// terminals copy selected lines. Returns an empty string when nothing is selected or no frame
        /// has been composed yet (for example on a non-interactive backend). Call on the application
        /// loop thread.
        /// </summary>
        /// <returns>The selected text, or an empty string. Never null.</returns>
        public string GetSelectedText()
        {
            if (!_SelActive || _LastRoot == null)
                return string.Empty;

            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            int top = Math.Min(_SelAnchorY, _SelFocusY);
            int bottom = Math.Max(_SelAnchorY, _SelFocusY);
            int rightExclusive = _SelRegionRect.Right;

            for (int y = top; y <= bottom; y++)
            {
                System.Text.StringBuilder row = new System.Text.StringBuilder();
                ComputeSelectionRowSpan(y, out int startX, out int endX);
                int x = startX;
                while (x <= endX && x < rightExclusive)
                {
                    Cell cell = _LastRoot.Get(x, y);
                    if (cell.IsContinuation)
                    {
                        x++;
                        continue;
                    }

                    row.Append(string.IsNullOrEmpty(cell.Grapheme) ? " " : cell.Grapheme);
                    x += cell.Width > 1 ? cell.Width : 1;
                }

                if (y != top)
                    builder.Append('\n');
                builder.Append(TrimTrailingSpaces(row.ToString()));
            }

            return builder.ToString();
        }

        /// <summary>
        /// Clears any active text selection and schedules a repaint so the highlight is removed. Safe to
        /// call when nothing is selected. Call on the application loop thread.
        /// </summary>
        public void ClearTextSelection()
        {
            ClearSelectionState();
            _Renderer?.Invalidate();
        }

        /// <summary>
        /// Gets or sets a callback invoked after every region, border, and widget has rendered (and before
        /// tooltips, modals, and toasts), so the application can draw on top of the finished layout:
        /// status decorations, link hints, or its own focus boxes around panes. Use
        /// <see cref="GetRegionBounds"/> and <see cref="GetRegionContentBounds"/> for region geometry and
        /// <see cref="CurrentFocusPath"/> for what holds focus. Several callbacks can be combined with
        /// <c>+=</c>.
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
        /// Gets or sets a value indicating whether every frame repaints all rows regardless of whether
        /// their content changed. Defaults to false, so the renderer emits only changed rows. Turn it on
        /// for backends that drop or corrupt incremental updates — for example some ConPTY / Windows
        /// Terminal configurations that leave stale cells behind — trading extra output for correctness.
        /// Setting it after <see cref="Start"/> takes effect on the next frame.
        /// </summary>
        public bool ForceFullRepaint
        {
            get { return _ForceFullRepaint; }
            set
            {
                _ForceFullRepaint = value;
                if (_Renderer != null)
                    _Renderer.ForceFullRepaint = value;
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
        /// Gets a value indicating whether the session is currently suspended by
        /// <see cref="SuspendAsync"/>. While suspended the render and input loops are inert and the
        /// terminal is handed back to whatever program the caller is running.
        /// </summary>
        public bool IsSuspended
        {
            get { return _Suspended; }
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
        /// Raised for a mouse event that no bound widget consumed. With hover tracking on
        /// (<see cref="MouseTrackingMode.AnyMotion"/>, the default) this includes every unconsumed
        /// pointer motion, which can be a high-volume stream — keep handlers cheap.
        /// </summary>
        public event Action<MouseEvent>? MouseReceived;

        /// <summary>
        /// Raised when the terminal window gains (<c>true</c>) or loses (<c>false</c>) focus, on
        /// terminals that support focus reporting (mode 1004; see
        /// <see cref="TerminalCapabilities.FocusReporting"/>). On focus loss the host also clears any
        /// hover state by delivering a Leave event to the hovered widget. Raised on the loop thread.
        /// </summary>
        public event Action<bool>? TerminalFocusChanged;

        /// <summary>
        /// Gets or sets the link registry the host hit-tests pointer motion against. When set, the
        /// host tracks the link under the pointer in <see cref="HoveredLink"/> and raises
        /// <see cref="LinkHovered"/> on changes, so an overlay can underline the hovered link or
        /// preview its URI in a status bar. Null (the default) disables link hover tracking. The
        /// registry is application-owned and typically rebuilt each frame.
        /// </summary>
        public LinkRegistry? Links
        {
            get { return _Links; }
            set { _Links = value; }
        }

        /// <summary>
        /// Gets the link currently under the pointer, or null when none (or when <see cref="Links"/>
        /// is not set). Updated on pointer motion and cleared on focus loss and capture shutoff.
        /// </summary>
        public Link? HoveredLink
        {
            get { return _HoveredLink; }
        }

        /// <summary>
        /// Raised when the link under the pointer changes: with the newly hovered <see cref="Link"/>,
        /// or null when the pointer leaves all links. Requires <see cref="Links"/> to be set. Raised
        /// on the loop thread.
        /// </summary>
        public event Action<Link?>? LinkHovered;

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
            if (_ApplyThemeToWidgets)
                ThemeApplier.Apply(widget, _Theme);
            _RenderRequested = true;

            if (widget is IFocusable && !_FocusOrder.Contains(regionId))
            {
                _FocusOrder.Add(regionId);
                if (_FocusedRegion == null)
                    SetFocusInternal(regionId);
            }
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
            _IncrementalRegions = true;
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
        /// Binds a key chord — or a two-chord sequence — directly to an action, replacing any existing
        /// binding. A convenience over <see cref="RegisterCommand"/> plus a routing-table registration
        /// for the common case where a config-file command id is not needed. Pass a single chord such as
        /// <c>"ctrl+q"</c>, or two space-separated chords such as <c>"ctrl+k ctrl+t"</c> for a sequence.
        /// </summary>
        /// <param name="chord">The chord string, or two space-separated chords for a sequence. Must not be null or empty.</param>
        /// <param name="action">The action to run. Must not be null.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="chord"/> is null, empty, or has more than two chords.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
        /// <exception cref="FormatException">Thrown when a chord token is not a recognized key or modifier.</exception>
        public void Bind(string chord, Action action)
        {
            if (string.IsNullOrEmpty(chord))
                throw new ArgumentException("Chord must not be null or empty.", nameof(chord));
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            string[] steps = chord.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (steps.Length == 2)
            {
                KeyChord first = KeyChord.Parse(steps[0]);
                KeyChord second = KeyChord.Parse(steps[1]);
                string sequenceId = "bind:" + first + " " + second;
                _Routing.UnregisterSequence(first, second);
                _Commands[sequenceId] = action;
                _Routing.RegisterSequence(first, second, sequenceId);
                return;
            }

            if (steps.Length != 1)
                throw new ArgumentException(
                    "A key binding must be a single chord or two space-separated chords.", nameof(chord));

            KeyChord parsed = KeyChord.Parse(steps[0]);
            string id = "bind:" + parsed;
            _Routing.Unregister(parsed);
            _Commands[id] = action;
            _Routing.Register(parsed, id);
        }

        /// <summary>
        /// Makes a status bar list the keys that work for whatever holds focus: on every frame it resolves
        /// <see cref="CurrentFocusPath"/> with <paramref name="resolver"/> and draws those hints ahead of the
        /// bar's fixed hints, which stay pinned at the end. While a text field has focus, keys that would
        /// type are hidden and the bar leads with how to leave the field (see <see cref="KeyHintResolver"/>).
        /// </summary>
        /// <param name="statusBar">The status bar. Must not be null.</param>
        /// <param name="resolver">The resolver to use, or null to create one with default settings.</param>
        /// <returns>The resolver in use, so application-wide hints and commands can be added to it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="statusBar"/> is null.</exception>
        public KeyHintResolver BindKeyHints(StatusBar statusBar, KeyHintResolver? resolver = null)
        {
            if (statusBar == null)
                throw new ArgumentNullException(nameof(statusBar));

            KeyHintResolver active = resolver ?? new KeyHintResolver();
            statusBar.HintSource = () => active.Resolve(_FocusPath);
            _RenderRequested = true;
            return active;
        }

        /// <summary>
        /// Requests the run loop to exit. Suitable as a method group for <see cref="Bind(string, Action)"/>.
        /// </summary>
        public void Quit()
        {
            RequestStop();
        }

        /// <summary>
        /// Moves keyboard focus to the widget bound to the supplied region. The region's widget must
        /// have been bound and must implement <see cref="IFocusable"/>. Notifies the previously and
        /// newly focused widgets (when they implement <see cref="IFocusAware"/>), updates
        /// <see cref="FocusContext"/>, and raises <see cref="FocusChanged"/>.
        /// </summary>
        /// <param name="regionId">The region id of the focusable widget. Must not be null or empty.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="regionId"/> is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the region is not a focusable bound widget.</exception>
        public void Focus(string regionId)
        {
            if (string.IsNullOrEmpty(regionId))
                throw new ArgumentException("Region id must not be null or empty.", nameof(regionId));
            if (!_FocusOrder.Contains(regionId))
                throw new InvalidOperationException("Region '" + regionId + "' is not a focusable bound widget.");

            SetFocusInternal(regionId);
        }

        /// <summary>
        /// Moves keyboard focus to the next focusable widget in tab order, wrapping around.
        /// </summary>
        /// <returns><c>true</c> when focus moved; <c>false</c> when no focusable widgets are bound.</returns>
        public bool FocusNext()
        {
            return MoveFocus(1);
        }

        /// <summary>
        /// Moves keyboard focus to the previous focusable widget in tab order, wrapping around.
        /// </summary>
        /// <returns><c>true</c> when focus moved; <c>false</c> when no focusable widgets are bound.</returns>
        public bool FocusPrevious()
        {
            return MoveFocus(-1);
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

            if (_ApplyThemeToWidgets)
                ThemeApplier.Apply(modal, _Theme);
            if (modal is DialogModal dialog)
                dialog.ApplyFocusTheme(_Theme);
            _Modals.Push(modal);
            _RenderRequested = true;
            return modal.Completion;
        }

        /// <summary>
        /// Shows a modal and returns its result cast to <typeparamref name="T"/>. When the modal closes
        /// with a value of another type (for example a cancel that yields null), the default value of
        /// <typeparamref name="T"/> is returned. Use this to avoid the untyped <see cref="object"/> cast
        /// that <see cref="ShowAsync(Modal)"/> otherwise requires at every custom modal call site.
        /// </summary>
        /// <typeparam name="T">The expected result type.</typeparam>
        /// <param name="modal">The modal. Must not be null.</param>
        /// <returns>The modal's result as <typeparamref name="T"/>, or the type default when it is not that type.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="modal"/> is null.</exception>
        public async Task<T?> ShowAsync<T>(Modal modal)
        {
            if (modal == null)
                throw new ArgumentNullException(nameof(modal));

            object? result = await ShowAsync(modal).ConfigureAwait(false);
            return result is T typed ? typed : default;
        }

        /// <summary>
        /// Queues an action to run on the application loop thread at the start of the next frame. Use
        /// this to marshal work back onto the render/input thread — for example from a modal
        /// continuation or a background task — so it can safely mutate UI state. Thread-safe; may be
        /// called from any thread. Queued actions run in the order posted, and any exception they throw
        /// propagates out of the loop.
        /// </summary>
        /// <param name="action">The action to run on the loop thread. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
        public void Post(Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            Activity? current = Activity.Current;
            ActivityContext parent = current != null ? current.Context : default(ActivityContext);
            _PostQueue.Enqueue(new PostedAction(action, parent, TuiKitInstruments.Timestamp()));
            TuiKitInstruments.Add(TuiKitInstruments.PostEnqueued, 1);
            TuiKitInstruments.Add(TuiKitInstruments.PostQueueDepth, 1);
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
        /// Shows the built-in command palette over a registry's commands. The chosen command's handler
        /// runs when the palette closes.
        /// </summary>
        /// <param name="registry">The command registry. Must not be null.</param>
        /// <returns>The chosen command, or null when cancelled.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="registry"/> is null.</exception>
        public async Task<Command?> ShowCommandPaletteAsync(CommandRegistry registry)
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));

            object? result = await ShowAsync(new CommandPaletteModal(registry)).ConfigureAwait(false);
            return result as Command;
        }

        /// <summary>
        /// Shows the built-in key-help overlay listing every registry command that has a chord, plus any
        /// extra rows.
        /// </summary>
        /// <param name="registry">The command registry. Must not be null.</param>
        /// <param name="extra">Additional rows (for example widget keys), or null.</param>
        /// <returns>A task that completes when the overlay closes.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="registry"/> is null.</exception>
        public async Task ShowKeyHelpAsync(CommandRegistry registry, IEnumerable<KeyHelpEntry>? extra = null)
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));

            KeyHelpModal modal = KeyHelpModal.FromCommands(registry.Commands);
            if (extra != null)
            {
                foreach (KeyHelpEntry entry in extra)
                {
                    if (entry != null)
                        modal.Add(entry.Category, entry.Keys, entry.Description);
                }
            }

            await ShowAsync(modal).ConfigureAwait(false);
        }

        /// <summary>
        /// Shows the notification history (the notification center) as a modal.
        /// </summary>
        /// <returns>A task that completes when the modal closes.</returns>
        public async Task ShowNotificationHistoryAsync()
        {
            await ShowAsync(new NotificationHistoryModal(_Notifications, () => NowMilliseconds)).ConfigureAwait(false);
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
            _RenderRequested = true;
        }

        /// <summary>
        /// Raises a notification with a title and action buttons. It stays in
        /// <see cref="NotificationCenter.History"/> after its toast goes away.
        /// </summary>
        /// <param name="text">The message. Must not be null.</param>
        /// <param name="severity">The severity.</param>
        /// <param name="title">An optional title, or null.</param>
        /// <param name="timeoutMilliseconds">The timeout, or null for the default. Zero is sticky.</param>
        /// <param name="actions">Action buttons, or none.</param>
        /// <returns>The notification.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public Notification Notify(string text, NotificationSeverity severity, string? title, int? timeoutMilliseconds, params NotificationAction[] actions)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            Notification notification = _Notifications.Add(text, severity, NowMilliseconds, timeoutMilliseconds, title, actions);
            _RenderRequested = true;
            return notification;
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
                TuiKitInstruments.Add(TuiKitInstruments.SessionStarts, 1, TuiKitTelemetryNames.AttrOutcome, TuiKitTelemetryNames.OutcomeRejected);
                throw new InvalidOperationException("The terminal is a singleton resource; only one TuiApplication may run at a time.");
            }

            _Started = true;
            Interlocked.Exchange(ref _TornDown, 0);
            _Clock.Start();
            _Backend.Start();

            _Renderer = new TerminalRenderer(
                Math.Max(1, _Backend.Size.Width),
                Math.Max(1, _Backend.Size.Height),
                _Backend.Capabilities.ColorDepth);
            _Renderer.SynchronizedOutput = _Backend.Capabilities.SynchronizedOutput;
            _Renderer.ForceFullRepaint = _ForceFullRepaint;

            _SessionStartTimestamp = TuiKitInstruments.Timestamp();
            TuiKitInstruments.Add(TuiKitInstruments.SessionStarts, 1, TuiKitTelemetryNames.AttrOutcome, TuiKitTelemetryNames.OutcomeOk);
            TuiKitInstruments.Add(TuiKitInstruments.SessionsActive, 1);
            TuiKitInstruments.SetSessionShape(_TargetFps, _Renderer.Size.Width, _Renderer.Size.Height);

            if (_Backend.IsInteractive)
            {
                EnterInteractiveModes();
                _Backend.Flush();

                InstallSafetyNet();
            }
        }

        // Writes the escape sequences that put the terminal into the interactive session state:
        // alternate screen, hidden cursor, and the mouse/focus/paste/enhanced-keyboard modes the
        // capabilities allow. Shared by Start and the resume half of SuspendAsync. The caller flushes.
        private void EnterInteractiveModes()
        {
            _Backend.Write(Ansi.EnterAltScreen);
            _Backend.Write(Ansi.HideCursor);
            if (_MouseCaptureEnabled)
            {
                string enableMouse = BuildMouseEnableSequence();
                if (enableMouse.Length > 0)
                    _Backend.Write(enableMouse);
            }

            if (_Backend.Capabilities.FocusReporting)
                _Backend.Write(Ansi.EnableFocusReporting);
            _Backend.Write(Ansi.EnableBracketedPaste);
            if (_Backend.Capabilities.EnhancedKeyboard)
                _Backend.Write(Ansi.PushKittyKeyboard);
        }

        // Writes the escape sequences that return the terminal to its pristine, cooked state: it undoes
        // everything EnterInteractiveModes turned on, in reverse, and closes any synchronized update
        // that a torn frame might have left open before leaving the alternate screen. Shared by Teardown
        // and the suspend half of SuspendAsync. The caller flushes.
        private void ExitInteractiveModes()
        {
            if (_Backend.Capabilities.EnhancedKeyboard)
                _Backend.Write(Ansi.PopKittyKeyboard);
            _Backend.Write(Ansi.DisableBracketedPaste);
            if (_Backend.Capabilities.FocusReporting)
                _Backend.Write(Ansi.DisableFocusReporting);
            _Backend.Write(Ansi.DisableMouse);
            _Backend.Write(Ansi.EndSynchronizedUpdate);
            _Backend.Write(Ansi.ShowCursor);
            _Backend.Write(Ansi.ExitAltScreen);
        }

        // Guarantees the terminal is handed back in a usable state on every exit path — a graceful
        // Quit, an unhandled exception, or a Ctrl+C the runtime would otherwise turn into an abrupt
        // process kill before Stop runs. Without this, the enable escapes emitted above (mouse
        // tracking, bracketed paste, the alternate screen) and the backend's raw console mode leak into
        // the shell: the scroll wheel spews SGR mouse reports and arrow keys echo their raw escapes.
        private void InstallSafetyNet()
        {
            // Cancel the runtime's default "kill the process" reaction to Ctrl+C and route it into the
            // ordinary stop request, so the run loop tears down through Stop like any other exit.
            _CancelKeyHandler = OnCancelKeyPress;
            Console.CancelKeyPress += _CancelKeyHandler;

            // Last-ditch restore for any exit that bypasses Stop (including the graceful shutdown the
            // runtime runs after an uncancelled Ctrl+C, which still raises ProcessExit).
            _ProcessExitHandler = OnProcessExit;
            AppDomain.CurrentDomain.ProcessExit += _ProcessExitHandler;
        }

        private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            // Ctrl+Break cannot be cancelled; leave it to the ProcessExit net. For Ctrl+C, suppress the
            // runtime's termination and let the loop exit cleanly through Stop.
            if (e.SpecialKey == ConsoleSpecialKey.ControlC)
            {
                e.Cancel = true;
                RequestStop();
            }
        }

        private void OnProcessExit(object? sender, EventArgs e)
        {
            Teardown();
        }

        /// <summary>
        /// Stops the session and restores the terminal to its original state. Safe to call multiple
        /// times and safe to race with the Ctrl+C and process-exit safety net installed by
        /// <see cref="Start"/>; the restore runs at most once per session.
        /// </summary>
        public void Stop()
        {
            Teardown();
        }

        // The single teardown path shared by Stop, Dispose, and the Ctrl+C/process-exit safety net.
        // Guarded so it runs exactly once per Start even when the run loop and a ProcessExit callback
        // race for it on different threads.
        private void Teardown()
        {
            if (!_Started)
                return;
            if (Interlocked.CompareExchange(ref _TornDown, 1, 0) != 0)
                return;

            _Running = false;

            if (_CancelKeyHandler != null)
            {
                Console.CancelKeyPress -= _CancelKeyHandler;
                _CancelKeyHandler = null;
            }

            if (_ProcessExitHandler != null)
            {
                AppDomain.CurrentDomain.ProcessExit -= _ProcessExitHandler;
                _ProcessExitHandler = null;
            }

            if (_Backend.IsInteractive)
            {
                try
                {
                    ExitInteractiveModes();
                    _Backend.Flush();
                }
                catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException || ex is NotSupportedException)
                {
                    TuiKitInstruments.RecordError(TuiKitTelemetryNames.ComponentTeardown, ex);

                    // Best effort during teardown: the output stream may already be closed or
                    // non-writable on process exit (disposed → ObjectDisposedException, closed for
                    // writing → NotSupportedException, transient I/O failure → IOException). A
                    // process-exit handler must never throw, so swallow these; the backend still
                    // restores its console mode below.
                }
            }

            _Backend.Stop();
            _Started = false;
            Interlocked.Decrement(ref _ActiveCount);

            TuiKitInstruments.Add(TuiKitInstruments.SessionsActive, -1);
            TuiKitInstruments.Record(TuiKitInstruments.SessionDuration, TuiKitInstruments.SecondsSince(_SessionStartTimestamp));
            TuiKitInstruments.SetSessionShape(0, 0, 0);
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
                    if (ShouldRenderFrame())
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
        /// Suspends the session, hands the terminal back in its pristine cooked state, runs
        /// <paramref name="whileSuspended"/> to completion, then restores the session and forces a full
        /// repaint. Use this to shell out to an external full-screen program — an editor, a pager, an
        /// interactive command — that needs the real terminal. Cross-platform: unlike a Ctrl+Z / SIGTSTP
        /// job-control suspend (which does not exist on Windows), this is driven entirely by the
        /// application and works identically on Windows, macOS, and Linux.
        /// </summary>
        /// <remarks>
        /// Must be called on the application loop thread — from a command handler or via
        /// <see cref="Post"/> — so it does not race the render loop. While suspended,
        /// <see cref="RenderOnce"/> and <see cref="PumpInputOnce"/> are inert, so a running
        /// <see cref="RunAsync"/> loop idles rather than painting over the external program. On a
        /// non-interactive backend, or before <see cref="Start"/>, the action simply runs with no
        /// terminal changes. If <paramref name="whileSuspended"/> throws, the terminal is restored
        /// before the exception propagates.
        /// </remarks>
        /// <param name="whileSuspended">The work to run while the terminal is handed back. Must not be null.</param>
        /// <param name="cancellationToken">A token observed before the terminal is handed back.</param>
        /// <returns>A task that completes when the action has run and the session has been restored.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="whileSuspended"/> is null.</exception>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled before suspending.</exception>
        public async Task SuspendAsync(Func<Task> whileSuspended, CancellationToken cancellationToken = default)
        {
            if (whileSuspended == null)
                throw new ArgumentNullException(nameof(whileSuspended));

            if (!_Started || !_Backend.IsInteractive)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await whileSuspended().ConfigureAwait(false);
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            long suspendStart = TuiKitInstruments.Timestamp();
            Activity? suspendSpan = TuiKitInstruments.StartActivity(TuiKitTelemetryNames.SpanSuspend);
            string suspendOutcome = TuiKitTelemetryNames.OutcomeOk;
            _Suspended = true;

            // Hand the terminal back: undo the session modes, then drop raw mode and stop the reader so
            // the external program owns stdin/stdout and the console line discipline as it expects.
            ExitInteractiveModes();
            _Backend.Flush();
            _Backend.Stop();

            try
            {
                await whileSuspended().ConfigureAwait(false);
                TuiKitInstruments.MarkOk(suspendSpan);
            }
            catch (Exception ex) when (RecordFailure(TuiKitTelemetryNames.ComponentSuspend, ex, suspendSpan, ref suspendOutcome))
            {
                throw;
            }
            finally
            {
                // Re-enter raw mode and the session state, then repaint the whole screen from scratch —
                // the external program left the terminal in an unknown state, so no diff is trustworthy.
                _Backend.Start();
                EnterInteractiveModes();
                _Backend.Flush();
                _Renderer?.Invalidate();
                _Suspended = false;
                TuiKitInstruments.Record(TuiKitInstruments.SuspendDuration, TuiKitInstruments.SecondsSince(suspendStart), TuiKitTelemetryNames.AttrOutcome, suspendOutcome);
                TuiKitInstruments.Stop(suspendSpan);
            }
        }

        /// <summary>
        /// Reads and dispatches any pending input once. Safe to call from a driving test.
        /// </summary>
        public void PumpInputOnce()
        {
            // While suspended the terminal belongs to whatever program the caller is running; the
            // backend's input path is torn down, so reading or dispatching would be meaningless.
            if (_Suspended)
                return;

            DrainPostQueue();

            int read = _Backend.ReadInput(_ReadBuffer, 0, _ReadBuffer.Length);
            if (read > 0)
            {
                TuiKitInstruments.Add(TuiKitInstruments.InputBytes, read);
                _Parser.Feed(_ReadBuffer, read);
                DispatchAll(_Parser.Drain());
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

            // Suspended: the terminal is in its cooked state for an external program. Painting a frame
            // now would corrupt that program's screen, so the loop idles until SuspendAsync resumes.
            if (_Suspended)
                return;

            DrainPostQueue();

            if (!_Backend.IsInteractive)
            {
                long lineStart = TuiKitInstruments.Timestamp();
                RenderLineMode();
                TuiKitInstruments.Record(TuiKitInstruments.FrameDuration, TuiKitInstruments.SecondsSince(lineStart), TuiKitTelemetryNames.AttrFrameOutcome, TuiKitTelemetryNames.FrameLineMode);
                TuiKitInstruments.Add(TuiKitInstruments.Frames, 1, TuiKitTelemetryNames.AttrFrameOutcome, TuiKitTelemetryNames.FrameLineMode);
                TuiKitInstruments.MarkFrameSuccess();
                return;
            }

            _Renderer.Render(_Backend, Compose);
            TuiKitInstruments.SetSessionShape(_TargetFps, _Renderer.Size.Width, _Renderer.Size.Height);
        }

        /// <summary>
        /// Returns a copy of the most recently rendered frame as cells, so a test can assert on the
        /// glyphs and styles the user would see (for example, that the focused region's border is drawn
        /// with heavy glyphs in the focus style) instead of matching raw terminal output. Call
        /// <see cref="RenderOnce"/> first. Read on the thread that renders.
        /// </summary>
        /// <returns>A copy of the last frame, or null before <see cref="Start"/> or when the backend is
        /// not interactive (line mode composes no frame).</returns>
        public CellBuffer? CaptureFrame()
        {
            if (_Renderer == null || !_Backend.IsInteractive)
                return null;

            return _Renderer.CopyLastFrame();
        }

        // Decides whether the run loop composes this frame. Without idle throttling every frame renders
        // (the original behavior). With it, a frame renders on activity, on a pane write, on request, or
        // once per idle interval.
        private bool ShouldRenderFrame()
        {
            long now = NowMilliseconds;
            if (_IdleFps <= 0)
            {
                _LastRenderMs = now;
                return true;
            }

            long versions = PaneVersions();
            if (versions != _LastPaneVersions)
            {
                _LastPaneVersions = versions;
                _LastActivityMs = now;
            }

            if (_RenderRequested)
            {
                _RenderRequested = false;
                _LastActivityMs = now;
            }

            bool idle = now - _LastActivityMs >= _IdleAfterMs;
            if (!idle || _LastRenderMs == long.MinValue || now - _LastRenderMs >= 1000 / _IdleFps
                || (_ShowTooltips && _PointerX >= 0 && now - _PointerStillSinceMs >= _TooltipDelayMs && now - _PointerStillSinceMs < _TooltipDelayMs + 1000))
            {
                _LastRenderMs = now;
                return true;
            }

            return false;
        }

        private long PaneVersions()
        {
            long total = 0;
            foreach (KeyValuePair<string, IWidget> entry in _Content)
            {
                if (entry.Value is Pane pane)
                    total += pane.Version;
            }

            return total;
        }

        private void ApplyThemeToAll()
        {
            foreach (KeyValuePair<string, IWidget> entry in _Content)
                ThemeApplier.Apply(entry.Value, _Theme);

            _Notifications.ApplyTheme(_Theme);
            _Tooltip.ApplyTheme(_Theme);
            Modal? top = _Modals.Top;
            ThemeApplier.Apply(top, _Theme);
        }

        private void RenderTooltip(ISurface root)
        {
            if (!_ShowTooltips || _PointerX < 0 || _Modals.IsActive)
                return;
            if (NowMilliseconds - _PointerStillSinceMs < _TooltipDelayMs)
                return;

            HitTestEntry? hit = HitTest(_PointerX, _PointerY);
            if (hit == null || !(hit.Widget is ITooltipProvider provider))
                return;

            string? text = provider.GetTooltip(_PointerX - hit.Rect.X, _PointerY - hit.Rect.Y);
            if (!string.IsNullOrEmpty(text))
                _Tooltip.Render(root, _PointerX, _PointerY, text!);
        }

        private void Compose(ISurface root)
        {
            Size size = root.Size;
            _FrameSize = size;
            _HitMap.Clear();
            RefreshFocusPath();

            if (_MouseTextSelectionEnabled)
            {
                // Keep the composited root so GetSelectedText can read cells back. A resize invalidates
                // the stored screen-cell rectangle, so drop any selection when the surface changes size.
                _LastRoot = root as BufferSurface;
                if (_SelActive && size != _LastComposeSize)
                    ClearSelectionState();
                _LastComposeSize = size;
            }

            if (_Layout != null && !_Layout.FitsIn(size))
            {
                LayoutBlockScreen.Render(root, _Layout.MinimumSize, size, _Theme.Text);
                return;
            }

            root.Fill(new Rect(0, 0, size.Width, size.Height), Cell.Blank(_Theme.Text));

            if (_Layout != null)
            {
                BufferSurface? bufferSurface = root as BufferSurface;
                if (_JoinRegionBorders)
                {
                    // Joined borders need every background down first, then every border, so shared edges
                    // can merge; the focused frame goes last so it stays whole over a shared line.
                    for (int i = 0; i < _Layout.Regions.Count; i++)
                        FillRegionBackground(root, _Layout.Regions[i], size);

                    Region? focusedRegion = null;
                    for (int i = 0; i < _Layout.Regions.Count; i++)
                    {
                        Region region = _Layout.Regions[i];
                        if (ShowsRegionFocus(region))
                            focusedRegion = region;
                        else
                            DrawRegionBorder(root, region, size, false, true);
                    }

                    if (focusedRegion != null)
                        DrawRegionBorder(root, focusedRegion, size, true, true);

                    for (int i = 0; i < _Layout.Regions.Count; i++)
                        RenderRegionContent(root, bufferSurface, _Layout.Regions[i], size);
                }
                else
                {
                    for (int i = 0; i < _Layout.Regions.Count; i++)
                    {
                        Region region = _Layout.Regions[i];
                        FillRegionBackground(root, region, size);
                        DrawRegionBorder(root, region, size, ShowsRegionFocus(region), false);
                        RenderRegionContent(root, bufferSurface, region, size);
                    }
                }
            }

            // Paint the highlight over the composed regions but under the overlay and modal layers, so
            // dialogs and overlays still draw on top of a selection.
            if (_MouseTextSelectionEnabled && _SelActive && root is BufferSurface selectionSurface)
                PaintSelection(selectionSurface);

            _OnRenderOverlay?.Invoke(root);

            RenderTooltip(root);

            if (_Modals.IsActive)
                _Modals.Render(root);

            if (_AutoRenderNotifications)
                _Notifications.Render(root, NowMilliseconds);
        }

        // A region shows the focused treatment while it holds focus, opted in, and no modal sits on top:
        // with a dialog open the panes behind it go plain so only the dialog reads as focused.
        private bool ShowsRegionFocus(Region region)
        {
            return region.HasBorder
                && (_HighlightFocusedRegion || region.FocusedBorder.HasValue)
                && string.Equals(region.Id, _FocusedRegion, StringComparison.Ordinal)
                && !_Modals.IsActive;
        }

        private void FillRegionBackground(ISurface root, Region region, Size size)
        {
            if (!region.HasBackground)
                return;

            Rect fill = region.Resolve(size).Intersect(new Rect(0, 0, size.Width, size.Height));
            if (!fill.IsEmpty)
                root.Fill(fill, Cell.Blank(ResolveRegionBackground(region)));
        }

        private void DrawRegionBorder(ISurface root, Region region, Size size, bool focused, bool join)
        {
            if (!region.HasBorder)
                return;

            Rect frame = region.Resolve(size).Intersect(new Rect(0, 0, size.Width, size.Height));
            if (_HighlightFocusedRegion || region.FocusedBorder.HasValue)
            {
                CellStyle focusStyle = FocusFrame.FocusedStyle(_Theme);
                FocusFrame.DrawCore(
                    root,
                    frame,
                    focused,
                    region.FocusedBorder ?? _FocusFrameOptions.FocusedBorder,
                    region.Border,
                    focusStyle,
                    _Theme.Border,
                    _Theme.Resolve(Theme.FocusTitleRole, focusStyle),
                    _Theme.UseAsciiBorders,
                    _FocusFrameOptions,
                    region.BorderTitle,
                    join);
                return;
            }

            BorderStyle borderStyle = _Theme.UseAsciiBorders ? BorderStyle.Ascii : region.Border;
            if (join)
                root.DrawJoinedBox(frame, _Theme.Border, borderStyle, region.BorderTitle);
            else
                root.DrawBox(frame, _Theme.Border, borderStyle, region.BorderTitle);
        }

        private void RenderRegionContent(ISurface root, BufferSurface? bufferSurface, Region region, Size size)
        {
            if (!_Content.TryGetValue(region.Id, out IWidget? widget))
                return;

            Rect rect = region.ContentRect(size).Intersect(new Rect(0, 0, size.Width, size.Height));
            if (rect.IsEmpty)
                return;

            _HitMap.Add(new HitTestEntry(region.Id, widget, rect));

            CellStyle regionBackground = ResolveRegionBackground(region);
            ISurface view = bufferSurface != null ? bufferSurface.CreateView(rect) : root;
            if (widget is Pane pane)
            {
                pane.Render(view, regionBackground);
            }
            else
            {
                view.Fill(new Rect(0, 0, rect.Width, rect.Height), Cell.Blank(regionBackground));
                widget.Render(view);
            }
        }

        private CellStyle ResolveRegionBackground(Region region)
        {
            if (region.Background.HasValue)
                return _Theme.Text.WithBackground(region.Background.Value);

            if (region.BackgroundRole != null)
                return _Theme.Text.WithBackground(_Theme.GetStyle(region.BackgroundRole).Background);

            return _Theme.Text;
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

        // Dispatches a drained batch, collapsing each run of consecutive pointer moves into its last
        // event. Any-motion tracking can report one move per cell traversed; only the newest position
        // matters, and presses/releases/wheel events act as barriers so ordering is preserved.
        private void DispatchAll(IReadOnlyList<InputEvent> events)
        {
            for (int i = 0; i < events.Count; i++)
            {
                if (IsCoalescableMove(events[i]))
                {
                    while (i + 1 < events.Count && IsCoalescableMove(events[i + 1]))
                    {
                        i++;
                        TuiKitInstruments.Add(TuiKitInstruments.InputCoalesced, 1);
                    }
                }

                Dispatch(events[i]);
            }
        }

        private static bool IsCoalescableMove(InputEvent inputEvent)
        {
            return inputEvent.Kind == InputEventKind.Mouse
                && inputEvent.Mouse != null
                && inputEvent.Mouse.Kind == MouseEventKind.Move;
        }

        private void Dispatch(InputEvent inputEvent)
        {
            _LastActivityMs = NowMilliseconds;
            _RenderRequested = true;
            string kind = InputKindName(inputEvent.Kind);
            long start = TuiKitInstruments.Timestamp();
            TuiKitInstruments.Add(TuiKitInstruments.InputEvents, 1, TuiKitTelemetryNames.AttrInputKind, kind);

            try
            {
                DispatchCore(inputEvent);
                RefreshFocusPath();
            }
            catch (Exception ex) when (RecordDispatchFailure(ex, kind, start))
            {
                throw;
            }

            TagList tags = new TagList();
            tags.Add(TuiKitTelemetryNames.AttrInputKind, kind);
            tags.Add(TuiKitTelemetryNames.AttrOutcome, TuiKitTelemetryNames.OutcomeOk);
            TuiKitInstruments.Record(TuiKitInstruments.InputDispatchDuration, TuiKitInstruments.SecondsSince(start), in tags);
        }

        private static bool RecordDispatchFailure(Exception ex, string kind, long start)
        {
            TagList tags = new TagList();
            tags.Add(TuiKitTelemetryNames.AttrInputKind, kind);
            tags.Add(TuiKitTelemetryNames.AttrOutcome, TuiKitTelemetryNames.OutcomeError);
            tags.Add(TuiKitTelemetryNames.AttrErrorType, TuiKitInstruments.ErrorType(ex));
            TuiKitInstruments.Record(TuiKitInstruments.InputDispatchDuration, TuiKitInstruments.SecondsSince(start), in tags);
            TuiKitInstruments.RecordError(TuiKitTelemetryNames.ComponentInput, ex);
            return false;
        }

        private static string InputKindName(InputEventKind kind)
        {
            switch (kind)
            {
                case InputEventKind.Key:
                    return "key";
                case InputEventKind.Mouse:
                    return "mouse";
                case InputEventKind.Paste:
                    return "paste";
                case InputEventKind.FocusGained:
                    return "focus_gained";
                case InputEventKind.FocusLost:
                    return "focus_lost";
                default:
                    return "other";
            }
        }

        private static void RouteKey(string route)
        {
            TuiKitInstruments.Add(TuiKitInstruments.KeyRoutes, 1, TuiKitTelemetryNames.AttrKeyRoute, route);
        }

        // Records a failure observed while running user or library code on the loop, marks the span,
        // and returns false so the caller's exception filter lets the exception propagate unchanged.
        private static bool RecordFailure(string component, Exception ex, Activity? span, ref string outcome)
        {
            outcome = TuiKitTelemetryNames.OutcomeError;
            TuiKitInstruments.RecordError(component, ex);
            TuiKitInstruments.MarkError(span, ex);
            return false;
        }

        private void DispatchCore(InputEvent inputEvent)
        {
            switch (inputEvent.Kind)
            {
                case InputEventKind.Key:
                    DispatchKey(inputEvent.Key);
                    break;
                case InputEventKind.Paste:
                    DispatchPaste(inputEvent.PasteText ?? string.Empty);
                    break;
                case InputEventKind.Mouse:
                    if (inputEvent.Mouse != null)
                        DispatchMouse(inputEvent.Mouse);
                    break;
                case InputEventKind.FocusGained:
                    TerminalFocusChanged?.Invoke(true);
                    break;
                case InputEventKind.FocusLost:
                    ClearHover(KeyModifiers.None);
                    TerminalFocusChanged?.Invoke(false);
                    break;
                default:
                    break;
            }
        }

        private void DispatchPaste(string text)
        {
            // Mirror the key path's "modal trap first" rule: while a modal owns the focus, its text field
            // receives the paste. Only when no modal is active does the paste surface to the application's
            // global PasteReceived handler. Without this, a bracketed paste into a prompt was decoded and
            // then silently dropped, because the modal stack was never offered the event.
            if (_Modals.IsActive)
            {
                _Modals.HandlePaste(text);
                return;
            }

            PasteReceived?.Invoke(text);
        }

        private void DispatchKey(KeyEvent key)
        {
            // Any key hides a pending or showing tooltip until the pointer moves again.
            _PointerStillSinceMs = long.MaxValue / 2;

            if (IsCtrlC(key) && _CtrlCPolicy != CtrlCPolicy.Custom)
            {
                // With the selection layer on and a selection present, Ctrl+C copies and clears it
                // instead of following the exit policy. Resetting the double-tap timer keeps a prior
                // lone Ctrl+C from combining with a later one across a copy.
                if (_MouseTextSelectionEnabled && _SelActive)
                {
                    RouteKey("selection_copy");
                    CopyTextSelection();
                    ClearTextSelection();
                    _LastCtrlC = long.MinValue;
                    return;
                }

                RouteKey("ctrl_c");
                HandleCtrlC();
                return;
            }

            // 1. Modal trap.
            if (_Modals.IsActive)
            {
                RouteKey("modal");
                _Modals.HandleKey(key);
                return;
            }

            // 2. Optional application pre-filter.
            Func<KeyEvent, bool>? filter = KeyFilter;
            if (filter != null && filter(key))
            {
                RouteKey("filter");
                return;
            }

            // Expire a stale pending sequence prefix so it never swallows a later key.
            if (_Router.HasPending && _PendingSinceMs != long.MinValue
                && NowMilliseconds - _PendingSinceMs > _SequenceTimeoutMs)
            {
                _Router.ResetPending();
                _PendingSinceMs = long.MinValue;
            }

            KeyChord chord = KeyChord.FromKeyEvent(key);

            // Complete or abandon a pending two-key sequence. When abandoned, the key falls through and
            // is processed normally rather than being swallowed.
            if (_Router.HasPending)
            {
                CommandResolution completion = _Router.TryCompletePending(chord);
                _PendingSinceMs = long.MinValue;
                if (completion.Status == CommandResolutionStatus.Command)
                {
                    RouteKey("sequence_command");
                    InvokeCommand(completion.CommandId);
                    return;
                }
            }

            // 3. Focus-scoped commands (an app explicitly scoped them to the current context).
            string? scoped = _Routing.ResolveFocusScoped(chord, _FocusContext);
            if (scoped != null)
            {
                RouteKey("scoped_command");
                InvokeCommand(scoped);
                return;
            }

            // 4. Focused widget gets first refusal on its own keys (fixes global-vs-widget collisions).
            IFocusable? focused = FocusedWidget;
            if (focused != null && focused.HandleKey(key))
            {
                RouteKey("widget");
                return;
            }

            // 5. Host focus traversal when the focused widget did not consume Tab.
            if (_FocusOrder.Count > 0 && key.Code == KeyCode.Tab)
            {
                RouteKey("focus_traversal");
                if ((key.Modifiers & KeyModifiers.Shift) != 0)
                    FocusPrevious();
                else
                    FocusNext();
                return;
            }

            // 6. Global commands: a sequence prefix begins a pending sequence; otherwise a single chord.
            if (_Routing.IsSequencePrefix(chord))
            {
                RouteKey("sequence_prefix");
                _Router.BeginPending(chord);
                _PendingSinceMs = NowMilliseconds;
                return;
            }

            string? global = _Routing.ResolveGlobalSingle(chord);
            if (global != null)
            {
                RouteKey("global_command");
                InvokeCommand(global);
                return;
            }

            // 7. Fallback for unconsumed keys.
            RouteKey("unhandled");
            KeyReceived?.Invoke(key);
        }

        private void DispatchMouse(MouseEvent mouse)
        {
            // Multi-click synthesis happens here rather than in the parser so the timing source is the
            // application clock and headless tests can drive it deterministically via PumpInputOnce.
            if (mouse.Kind == MouseEventKind.Press)
            {
                int clickCount = _ClickSynthesizer.RegisterPress(mouse.Button, mouse.X, mouse.Y, NowMilliseconds);
                if (clickCount != mouse.ClickCount)
                    mouse = mouse.WithClickCount(clickCount);
            }

            UpdateLinkHover(mouse);

            if (mouse.X != _PointerX || mouse.Y != _PointerY || mouse.Kind != MouseEventKind.Move)
            {
                _PointerX = mouse.Kind == MouseEventKind.Leave ? -1 : mouse.X;
                _PointerY = mouse.Y;
                _PointerStillSinceMs = mouse.Kind == MouseEventKind.Move ? NowMilliseconds : long.MaxValue / 2;
            }

            // Toasts draw above everything, so clicks on their actions or dismiss markers go to the
            // notification center first. Clicks elsewhere (and on plain toasts) fall through as before.
            if (_AutoRenderNotifications && mouse.Kind == MouseEventKind.Press && _Notifications.HandleMouse(mouse))
                return;

            // Modal trap: while a modal is active it receives the mouse before any region-bound widget,
            // mirroring the key trap above, so clicks land on the dialog and never leak to the interface
            // behind it. The event is consumed here whether or not the modal acts on it.
            if (_Modals.IsActive)
            {
                _Modals.HandleMouse(mouse);
                return;
            }

            // Built-in text selection runs before widget routing so a drag over a widget that consumes
            // mouse (a TextEditor, a scrollable pane) is still captured as a selection. It is inert
            // unless enabled and the mouse is captured; a press is never consumed, so click-to-focus and
            // caret placement still work.
            if (_MouseTextSelectionEnabled && _MouseCaptureEnabled && HandleSelectionMouse(mouse))
                return;

            if (_EnableMouseRouting && RouteMouse(mouse))
                return;

            MouseReceived?.Invoke(mouse);
        }

        private bool RouteMouse(MouseEvent mouse)
        {
            HitTestEntry? hit = HitTest(mouse.X, mouse.Y);
            UpdateHover(hit, mouse);

            if (hit == null)
                return false;

            // Click-to-focus is a side effect; it does not, by itself, swallow the event.
            if (mouse.Kind == MouseEventKind.Press && hit.Widget is IFocusable && _FocusOrder.Contains(hit.RegionId))
                SetFocusInternal(hit.RegionId);

            if (hit.Widget is IMouseAware aware)
            {
                MouseEvent local = new MouseEvent(
                    mouse.Kind, mouse.Button, mouse.X - hit.Rect.X, mouse.Y - hit.Rect.Y, mouse.Modifiers, mouse.ClickCount);
                return aware.HandleMouse(local);
            }

            return false;
        }

        // Drives the built-in selection state machine. Returns true only when it consumes the event: a
        // drag move that updates the selection, or the release that ends one. A press is never consumed
        // so click-to-focus and caret placement still route normally.
        private bool HandleSelectionMouse(MouseEvent mouse)
        {
            // A wheel scrolls content out from under a screen-cell selection, so its highlighted cells
            // would no longer mean what they did. Drop the selection but let the wheel scroll.
            if (mouse.Kind == MouseEventKind.Wheel)
            {
                if (_SelActive)
                    ClearTextSelection();
                return false;
            }

            if (mouse.Button != MouseButton.Left && mouse.Kind != MouseEventKind.Release)
                return false;

            switch (mouse.Kind)
            {
                case MouseEventKind.Press:
                    // A new gesture discards the previous selection, whether in the same region or another.
                    ClearSelectionState();
                    HitTestEntry? hit = HitTest(mouse.X, mouse.Y);
                    if (hit == null)
                        return false;

                    _SelRegionRect = hit.Rect;
                    _SelAnchorX = ClampInt(mouse.X, hit.Rect.Left, hit.Rect.Right - 1);
                    _SelAnchorY = ClampInt(mouse.Y, hit.Rect.Top, hit.Rect.Bottom - 1);
                    _SelFocusX = _SelAnchorX;
                    _SelFocusY = _SelAnchorY;
                    _SelDragging = true;
                    _SelMoved = false;
                    _SelActive = false;
                    return false;

                case MouseEventKind.Move:
                    if (!_SelDragging || mouse.Button != MouseButton.Left)
                        return false;

                    int focusX = ClampInt(mouse.X, _SelRegionRect.Left, _SelRegionRect.Right - 1);
                    int focusY = ClampInt(mouse.Y, _SelRegionRect.Top, _SelRegionRect.Bottom - 1);
                    if (!_SelMoved && (focusX != _SelAnchorX || focusY != _SelAnchorY))
                        _SelMoved = true;

                    _SelFocusX = focusX;
                    _SelFocusY = focusY;
                    _SelActive = _SelMoved;
                    if (_SelActive)
                    {
                        _Renderer?.Invalidate();
                        return true;
                    }

                    return false;

                case MouseEventKind.Release:
                    if (_SelDragging && _SelActive)
                    {
                        // Finalize; keep the selection active so a later Ctrl+C can copy it.
                        _SelDragging = false;
                        return true;
                    }

                    // A plain click with no drag: drop the pending anchor and let the release route.
                    _SelDragging = false;
                    _SelActive = false;
                    return false;

                default:
                    return false;
            }
        }

        private void ClearSelectionState()
        {
            _SelActive = false;
            _SelDragging = false;
            _SelMoved = false;
        }

        // Computes the inclusive [startX, endX] column span selected on a given row, wrapping within the
        // anchor region's horizontal bounds. Shared by PaintSelection and GetSelectedText so they agree.
        private void ComputeSelectionRowSpan(int y, out int startX, out int endX)
        {
            int sx = _SelAnchorX;
            int sy = _SelAnchorY;
            int ex = _SelFocusX;
            int ey = _SelFocusY;
            if (sy > ey || (sy == ey && sx > ex))
            {
                int swapX = sx;
                sx = ex;
                ex = swapX;
                int swapY = sy;
                sy = ey;
                ey = swapY;
            }

            startX = (y == sy) ? sx : _SelRegionRect.Left;
            endX = (y == ey) ? ex : _SelRegionRect.Right - 1;
        }

        private void PaintSelection(BufferSurface surface)
        {
            int top = Math.Min(_SelAnchorY, _SelFocusY);
            int bottom = Math.Max(_SelAnchorY, _SelFocusY);
            int rightExclusive = _SelRegionRect.Right;

            for (int y = top; y <= bottom; y++)
            {
                ComputeSelectionRowSpan(y, out int startX, out int endX);
                for (int x = startX; x <= endX && x < rightExclusive; x++)
                {
                    Cell under = surface.Get(x, y);

                    // The leading cell of a wide glyph carries the whole two-column glyph and its reverse
                    // attribute covers both columns; painting the continuation would double the grapheme.
                    if (under.IsContinuation)
                        continue;

                    string grapheme = string.IsNullOrEmpty(under.Grapheme) ? " " : under.Grapheme;
                    int width = under.Width > 0 ? under.Width : 1;
                    CellStyle style = SelectionStyle ?? under.Style.WithAttribute(CellAttributes.Reverse, true);
                    surface.Set(x, y, Cell.Glyph(grapheme, style, width));
                }
            }
        }

        private void CopyTextSelection()
        {
            string text = GetSelectedText();
            if (text.Length == 0)
                return;

            if (_Backend.IsInteractive)
            {
                _Backend.Write(ClipboardWriter.BuildSequence(text));
                _Backend.Flush();
                TuiKitInstruments.Add(TuiKitInstruments.ClipboardWrites, 1);
            }

            TextCopied?.Invoke(text);
        }

        private static string TrimTrailingSpaces(string value)
        {
            int end = value.Length;
            while (end > 0 && value[end - 1] == ' ')
                end--;

            return value.Substring(0, end);
        }

        // Synthesizes hover transitions from hit-test changes. The contract, in order: Leave to the
        // previously hovered widget, Enter to the newly hovered widget, then the triggering event via
        // the caller. Enter/Leave return values are ignored — they never swallow the triggering event.
        private void UpdateHover(HitTestEntry? hit, MouseEvent mouse)
        {
            if (hit != null && string.Equals(hit.RegionId, _HoverRegionId, StringComparison.Ordinal))
            {
                // Same region; refresh the geometry in case a relayout moved it between events.
                _HoverWidget = hit.Widget;
                _HoverRect = hit.Rect;
                return;
            }

            if (hit == null && _HoverRegionId == null)
                return;

            DeliverLeave(mouse.Modifiers, mouse.X, mouse.Y);

            if (hit != null)
            {
                if (hit.Widget is IMouseAware aware)
                {
                    MouseEvent enter = new MouseEvent(
                        MouseEventKind.Enter,
                        MouseButton.None,
                        ClampInt(mouse.X - hit.Rect.X, 0, hit.Rect.Width - 1),
                        ClampInt(mouse.Y - hit.Rect.Y, 0, hit.Rect.Height - 1),
                        mouse.Modifiers,
                        0);
                    aware.HandleMouse(enter);
                }

                _HoverRegionId = hit.RegionId;
                _HoverWidget = hit.Widget;
                _HoverRect = hit.Rect;
            }
        }

        // Delivers a Leave to the hovered widget (coordinates clamped into its last known content
        // rectangle) and clears the hover state. Used for region transitions, terminal focus loss, and
        // mouse capture/tracking shutoff, where no meaningful pointer position may exist.
        private void DeliverLeave(KeyModifiers modifiers, int pointerX, int pointerY)
        {
            if (_HoverWidget is IMouseAware aware && _HoverRect.Width > 0 && _HoverRect.Height > 0)
            {
                MouseEvent leave = new MouseEvent(
                    MouseEventKind.Leave,
                    MouseButton.None,
                    ClampInt(pointerX - _HoverRect.X, 0, _HoverRect.Width - 1),
                    ClampInt(pointerY - _HoverRect.Y, 0, _HoverRect.Height - 1),
                    modifiers,
                    0);
                aware.HandleMouse(leave);
            }

            _HoverRegionId = null;
            _HoverWidget = null;
            _HoverRect = default;
        }

        private void ClearHover(KeyModifiers modifiers)
        {
            if (_HoveredLink != null)
            {
                _HoveredLink = null;
                LinkHovered?.Invoke(null);
            }

            if (_HoverRegionId == null)
                return;

            DeliverLeave(modifiers, _HoverRect.X, _HoverRect.Y);
        }

        private void UpdateLinkHover(MouseEvent mouse)
        {
            if (_Links == null)
                return;
            if (mouse.Kind != MouseEventKind.Move && mouse.Kind != MouseEventKind.Press)
                return;

            Link? link = _Links.HitTest(mouse.X, mouse.Y);
            if (!ReferenceEquals(link, _HoveredLink))
            {
                _HoveredLink = link;
                LinkHovered?.Invoke(link);
            }
        }

        private string BuildMouseEnableSequence()
        {
            switch (_MouseTrackingMode)
            {
                case MouseTrackingMode.None:
                    return string.Empty;
                case MouseTrackingMode.ButtonsAndDrag:
                    return Ansi.EnableMouse + Ansi.DisableAnyMotion;
                default:
                    return Ansi.EnableMouse;
            }
        }

        private static int ClampInt(int value, int min, int max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;

            return value;
        }

        private HitTestEntry? HitTest(int x, int y)
        {
            Point point = new Point(x, y);
            for (int i = _HitMap.Count - 1; i >= 0; i--)
            {
                if (_HitMap[i].Rect.Contains(point))
                    return _HitMap[i];
            }

            return null;
        }

        private IFocusable? FocusedWidget
        {
            get
            {
                if (_FocusedRegion != null && _Content.TryGetValue(_FocusedRegion, out IWidget? widget) && widget is IFocusable focusable)
                    return focusable;

                return null;
            }
        }

        private bool MoveFocus(int direction)
        {
            if (_FocusOrder.Count == 0)
                return false;

            int current = _FocusedRegion != null ? _FocusOrder.IndexOf(_FocusedRegion) : -1;
            int next = current < 0
                ? (direction > 0 ? 0 : _FocusOrder.Count - 1)
                : (current + direction + _FocusOrder.Count) % _FocusOrder.Count;

            // Skip disabled and hidden widgets, and regions not on screen, unless every candidate is.
            for (int attempt = 0; attempt < _FocusOrder.Count; attempt++)
            {
                int candidate = (next + (direction * attempt) + (_FocusOrder.Count * _FocusOrder.Count)) % _FocusOrder.Count;
                if (_Content.TryGetValue(_FocusOrder[candidate], out IWidget? widget) && FocusScope.IsFocusable(widget) && IsRegionShown(_FocusOrder[candidate]))
                {
                    next = candidate;
                    break;
                }
            }

            SetFocusInternal(_FocusOrder[next]);

            // Entering a hierarchical container focuses its first descendant (forward) or last (backward).
            if (_Content.TryGetValue(_FocusOrder[next], out IWidget? entered) && entered is IFocusContainer container)
                container.FocusEdge(direction > 0);

            return true;
        }

        private void SetFocusInternal(string? regionId)
        {
            if (string.Equals(_FocusedRegion, regionId, StringComparison.Ordinal))
                return;

            if (_FocusedRegion != null && _Content.TryGetValue(_FocusedRegion, out IWidget? previous) && previous is IFocusAware previousAware)
                previousAware.OnFocusChanged(false);

            _FocusedRegion = regionId;

            if (regionId != null)
            {
                _FocusContext = regionId;
                if (_Content.TryGetValue(regionId, out IWidget? next) && next is IFocusAware nextAware)
                    nextAware.OnFocusChanged(true);
            }

            _Renderer?.Invalidate();
            FocusChanged?.Invoke(regionId);
            RefreshFocusPath();
        }

        private Region? FindRegion(string regionId)
        {
            if (string.IsNullOrEmpty(regionId))
                throw new ArgumentException("Region id must not be null or empty.", nameof(regionId));

            return _Layout?.FindById(regionId);
        }

        // A region is on screen when the current layout has it and, once a frame has been composed, its
        // content area is not empty. A widget bound to a region the layout dropped (a hidden sidebar) is
        // not a focus stop.
        internal bool IsRegionShown(string regionId)
        {
            if (_Layout == null)
                return true;

            Region? region = _Layout.FindById(regionId);
            if (region == null)
                return false;
            if (_FrameSize.Width <= 0 || _FrameSize.Height <= 0)
                return true;

            return !region.ContentRect(_FrameSize).Intersect(new Rect(0, 0, _FrameSize.Width, _FrameSize.Height)).IsEmpty;
        }

        internal void InjectInput(InputEvent inputEvent)
        {
            Dispatch(inputEvent);
        }

        internal string HitMapSignature()
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            for (int i = 0; i < _HitMap.Count; i++)
            {
                HitTestEntry entry = _HitMap[i];
                builder.Append(entry.RegionId).Append(':')
                    .Append(entry.Rect.X).Append(',').Append(entry.Rect.Y).Append(',')
                    .Append(entry.Rect.Width).Append(',').Append(entry.Rect.Height).Append(';');
            }

            return builder.ToString();
        }

        // Rebuilds the focus path into a reusable scratch list and publishes a new snapshot only when it
        // differs, so the common case (nothing moved) allocates nothing.
        private void RefreshFocusPath()
        {
            _FocusScratch.Clear();
            string? region = _FocusedRegion;
            if (region != null && _Content.TryGetValue(region, out IWidget? root))
                FocusPath.Collect(root, _FocusScratch);
            else
                region = null;

            if (_FocusPath.SameAs(region, _FocusScratch))
                return;

            _FocusPath = _FocusScratch.Count == 0 ? FocusPath.Empty : new FocusPath(region, new List<object>(_FocusScratch));
            _RenderRequested = true;
            FocusPathChanged?.Invoke(_FocusPath);
        }

        private void DrainPostQueue()
        {
            bool ran = false;
            while (_PostQueue.TryDequeue(out PostedAction? posted))
            {
                RunPosted(posted);
                _RenderRequested = true;
                ran = true;
            }

            if (ran)
                RefreshFocusPath();
        }

        private static void RunPosted(PostedAction posted)
        {
            TuiKitInstruments.Add(TuiKitInstruments.PostQueueDepth, -1);
            TuiKitInstruments.Record(TuiKitInstruments.PostQueueWait, TuiKitInstruments.SecondsSince(posted.EnqueuedTimestamp));

            long start = TuiKitInstruments.Timestamp();
            string outcome = TuiKitTelemetryNames.OutcomeOk;
            Activity? span = posted.ParentContext != default(ActivityContext)
                ? TuiKitInstruments.StartActivity(TuiKitTelemetryNames.SpanPost, ActivityKind.Internal, posted.ParentContext)
                : TuiKitInstruments.StartActivity(TuiKitTelemetryNames.SpanPost);

            try
            {
                posted.Action();
                TuiKitInstruments.MarkOk(span);
            }
            catch (Exception ex) when (RecordFailure(TuiKitTelemetryNames.ComponentPost, ex, span, ref outcome))
            {
                throw;
            }
            finally
            {
                TuiKitInstruments.Record(TuiKitInstruments.PostDuration, TuiKitInstruments.SecondsSince(start), TuiKitTelemetryNames.AttrOutcome, outcome);
                TuiKitInstruments.Stop(span);
            }
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
                    // Guard against the sentinel: with no prior press, _LastCtrlC is long.MinValue and
                    // (now - long.MinValue) overflows to a small value that would spuriously satisfy the
                    // window, exiting on the very first Ctrl+C. Only a real prior timestamp counts.
                    if (_LastCtrlC != long.MinValue && now - _LastCtrlC <= 500)
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

            if (!_Commands.TryGetValue(commandId, out Action? handler))
            {
                TuiKitInstruments.Add(TuiKitInstruments.CommandInvocations, 1, TuiKitTelemetryNames.AttrOutcome, TuiKitTelemetryNames.OutcomeUnregistered);
                return;
            }

            long start = TuiKitInstruments.Timestamp();
            string outcome = TuiKitTelemetryNames.OutcomeOk;
            Activity? span = TuiKitInstruments.StartActivity(TuiKitTelemetryNames.SpanCommand);
            TuiKitInstruments.SetTag(span, TuiKitTelemetryNames.AttrCommandId, commandId);

            try
            {
                handler();
                TuiKitInstruments.MarkOk(span);
            }
            catch (Exception ex) when (RecordCommandFailure(ex, span, ref outcome))
            {
                throw;
            }
            finally
            {
                TuiKitInstruments.Record(TuiKitInstruments.CommandDuration, TuiKitInstruments.SecondsSince(start), TuiKitTelemetryNames.AttrOutcome, outcome);
                if (outcome == TuiKitTelemetryNames.OutcomeOk)
                    TuiKitInstruments.Add(TuiKitInstruments.CommandInvocations, 1, TuiKitTelemetryNames.AttrOutcome, outcome);
                TuiKitInstruments.Stop(span);
            }
        }

        private static bool RecordCommandFailure(Exception ex, Activity? span, ref string outcome)
        {
            TagList tags = new TagList();
            tags.Add(TuiKitTelemetryNames.AttrOutcome, TuiKitTelemetryNames.OutcomeError);
            tags.Add(TuiKitTelemetryNames.AttrErrorType, TuiKitInstruments.ErrorType(ex));
            TuiKitInstruments.Add(TuiKitInstruments.CommandInvocations, 1, in tags);
            return RecordFailure(TuiKitTelemetryNames.ComponentCommand, ex, span, ref outcome);
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

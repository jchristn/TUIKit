namespace TUIKit.Modals
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Diagnostics;
    using TUIKit.Input;
    using TUIKit.Theming;
    using TUIKit.Unicode;

    /// <summary>
    /// Collects and renders transient notifications (toasts) and keeps a bounded history of them (the
    /// notification center). Toasts stack in the top-right corner, newest first, expire on their timeout,
    /// and never take focus. A configurable maximum caps how many are shown at once; older toasts are
    /// dropped from the screen when the cap is exceeded but stay in <see cref="History"/>.
    /// </summary>
    /// <remarks>
    /// Toasts can carry a title and action buttons, wrap over several lines (<see cref="MaxToastLines"/>),
    /// be wider (<see cref="ToastWidth"/>), and show a dismiss marker (<see cref="ShowDismissButton"/>).
    /// Clicks on action labels, the dismiss marker, or (with <see cref="DismissOnClick"/>) the toast body
    /// are handled by <see cref="HandleMouse"/>, which the host calls before routing the mouse anywhere
    /// else. The defaults reproduce the original single-line, 40-column toasts. All members are
    /// thread-safe; callbacks (<see cref="Changed"/>, action callbacks) run outside the lock on the calling
    /// thread.
    /// </remarks>
    public sealed class NotificationCenter : IThemeable
    {
        private readonly object _Sync = new object();
        private readonly List<Notification> _Items = new List<Notification>();
        private readonly List<Notification> _History = new List<Notification>();
        private readonly List<ToastHitRegion> _Hits = new List<ToastHitRegion>();
        private int _MaxConcurrent = 5;
        private int _DefaultTimeoutMilliseconds = 4000;
        private int _HistoryLimit = 100;
        private int _ToastWidth = 40;
        private int _MaxToastLines = 1;
        private long _NextId = 1;
        private CellStyle _BackgroundStyle = CellStyle.Default;
        private CellStyle _InfoStyle = CellStyle.Default.WithForeground(Color.FromPalette(6));
        private CellStyle _SuccessStyle = CellStyle.Default.WithForeground(Color.FromPalette(2));
        private CellStyle _WarningStyle = CellStyle.Default.WithForeground(Color.FromPalette(3));
        private CellStyle _ErrorStyle = CellStyle.Default.WithForeground(Color.FromPalette(1));
        private CellStyle _ActionStyle = CellStyle.Default.WithAttribute(CellAttributes.Underline, true);
        private bool _ShowDismissButton;
        private bool _DismissOnClick;

        /// <summary>
        /// Raised after a notification is added, dismissed, read, or removed, or the history changes.
        /// Runs outside the center's lock on the thread that made the change.
        /// </summary>
        public event Action? Changed;

        /// <summary>
        /// Gets or sets the maximum number of notifications shown at once. Defaults to 5. Must be
        /// greater than zero.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a non-positive value.</exception>
        public int MaxConcurrent
        {
            get { lock (_Sync) { return _MaxConcurrent; } }
            set
            {
                if (value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Maximum must be greater than zero.");
                lock (_Sync) { _MaxConcurrent = value; }
            }
        }

        /// <summary>
        /// Gets or sets the default timeout in milliseconds used when none is specified. Defaults to
        /// 4000. Must be zero or greater (zero means sticky).
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a negative value.</exception>
        public int DefaultTimeoutMilliseconds
        {
            get { lock (_Sync) { return _DefaultTimeoutMilliseconds; } }
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Timeout must be zero or greater.");
                lock (_Sync) { _DefaultTimeoutMilliseconds = value; }
            }
        }

        /// <summary>
        /// Gets or sets how many notifications <see cref="History"/> keeps; the oldest are dropped first.
        /// Defaults to 100. Zero keeps no history. Must be zero or greater.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a negative value.</exception>
        public int HistoryLimit
        {
            get { lock (_Sync) { return _HistoryLimit; } }
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "History limit must be zero or greater.");
                lock (_Sync)
                {
                    _HistoryLimit = value;
                    TrimHistory();
                }
            }
        }

        /// <summary>
        /// Gets or sets the toast width in columns, including the one-column padding on each side. The
        /// toast never exceeds the screen width minus two. Defaults to 40. Must be at least 4.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set below 4.</exception>
        public int ToastWidth
        {
            get { lock (_Sync) { return _ToastWidth; } }
            set
            {
                if (value < 4)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Toast width must be at least 4.");
                lock (_Sync) { _ToastWidth = value; }
            }
        }

        /// <summary>
        /// Gets or sets the maximum number of wrapped text lines per toast (the title and the action row
        /// are extra). Defaults to 1, the original single-line toast: longer text is truncated with an
        /// ellipsis. Must be at least 1.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set below 1.</exception>
        public int MaxToastLines
        {
            get { lock (_Sync) { return _MaxToastLines; } }
            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Toast lines must be at least 1.");
                lock (_Sync) { _MaxToastLines = value; }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether each toast shows an <c>x</c> dismiss marker at its
        /// top-right corner. Defaults to false.
        /// </summary>
        public bool ShowDismissButton
        {
            get { lock (_Sync) { return _ShowDismissButton; } }
            set { lock (_Sync) { _ShowDismissButton = value; } }
        }

        /// <summary>
        /// Gets or sets a value indicating whether a click anywhere on a toast dismisses it. Defaults to
        /// false, so clicks on toasts without actions or a dismiss marker pass through as before.
        /// </summary>
        public bool DismissOnClick
        {
            get { lock (_Sync) { return _DismissOnClick; } }
            set { lock (_Sync) { _DismissOnClick = value; } }
        }

        /// <summary>
        /// Gets or sets the style painted behind each toast. Defaults to <see cref="CellStyle.Default"/>.
        /// </summary>
        public CellStyle BackgroundStyle
        {
            get { lock (_Sync) { return _BackgroundStyle; } }
            set { lock (_Sync) { _BackgroundStyle = value; } }
        }

        /// <summary>
        /// Gets or sets the style of info toasts. Defaults to a cyan (palette 6) foreground.
        /// </summary>
        public CellStyle InfoStyle
        {
            get { lock (_Sync) { return _InfoStyle; } }
            set { lock (_Sync) { _InfoStyle = value; } }
        }

        /// <summary>
        /// Gets or sets the style of success toasts. Defaults to a green (palette 2) foreground.
        /// </summary>
        public CellStyle SuccessStyle
        {
            get { lock (_Sync) { return _SuccessStyle; } }
            set { lock (_Sync) { _SuccessStyle = value; } }
        }

        /// <summary>
        /// Gets or sets the style of warning toasts. Defaults to a yellow (palette 3) foreground.
        /// </summary>
        public CellStyle WarningStyle
        {
            get { lock (_Sync) { return _WarningStyle; } }
            set { lock (_Sync) { _WarningStyle = value; } }
        }

        /// <summary>
        /// Gets or sets the style of error toasts. Defaults to a red (palette 1) foreground.
        /// </summary>
        public CellStyle ErrorStyle
        {
            get { lock (_Sync) { return _ErrorStyle; } }
            set { lock (_Sync) { _ErrorStyle = value; } }
        }

        /// <summary>
        /// Gets or sets the style overlaid on action labels. Defaults to underlined.
        /// </summary>
        public CellStyle ActionStyle
        {
            get { lock (_Sync) { return _ActionStyle; } }
            set { lock (_Sync) { _ActionStyle = value; } }
        }

        /// <summary>
        /// Gets the notification history, newest first, including dismissed and expired notifications.
        /// Returns a snapshot. Never null.
        /// </summary>
        public IReadOnlyList<Notification> History
        {
            get
            {
                lock (_Sync)
                {
                    List<Notification> result = new List<Notification>(_History.Count);
                    for (int i = _History.Count - 1; i >= 0; i--)
                        result.Add(_History[i]);

                    return result;
                }
            }
        }

        /// <summary>
        /// Gets the number of notifications in <see cref="History"/> that are not read.
        /// </summary>
        public int UnreadCount
        {
            get
            {
                lock (_Sync)
                {
                    int count = 0;
                    for (int i = 0; i < _History.Count; i++)
                    {
                        if (!_History[i].IsRead)
                            count++;
                    }

                    return count;
                }
            }
        }

        /// <summary>
        /// Adds a notification.
        /// </summary>
        /// <param name="text">The text. Must not be null.</param>
        /// <param name="severity">The severity.</param>
        /// <param name="nowMilliseconds">The current time in milliseconds.</param>
        /// <param name="timeoutMilliseconds">The timeout, or null to use the default.</param>
        /// <returns>The created notification.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public Notification Add(string text, NotificationSeverity severity, long nowMilliseconds, int? timeoutMilliseconds = null)
        {
            return Add(text, severity, nowMilliseconds, timeoutMilliseconds, null, null);
        }

        /// <summary>
        /// Adds a notification with an optional title and action buttons.
        /// </summary>
        /// <param name="text">The text. Must not be null.</param>
        /// <param name="severity">The severity.</param>
        /// <param name="nowMilliseconds">The current time in milliseconds.</param>
        /// <param name="timeoutMilliseconds">The timeout, or null to use the default. Zero is sticky.</param>
        /// <param name="title">An optional title, or null.</param>
        /// <param name="actions">Optional action buttons, or null.</param>
        /// <returns>The created notification.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> or an action is null.</exception>
        public Notification Add(string text, NotificationSeverity severity, long nowMilliseconds, int? timeoutMilliseconds, string? title, IEnumerable<NotificationAction>? actions)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            Notification notification;
            lock (_Sync)
            {
                int timeout = timeoutMilliseconds ?? _DefaultTimeoutMilliseconds;
                notification = new Notification(text, severity, nowMilliseconds, timeout, title, actions);
                notification.Id = _NextId++;
                _Items.Add(notification);
                TuiKitInstruments.Add(TuiKitInstruments.Notifications, 1, TuiKitTelemetryNames.AttrSeverity, SeverityName(severity));

                while (_Items.Count > _MaxConcurrent)
                {
                    _Items.RemoveAt(0);
                    TuiKitInstruments.Add(TuiKitInstruments.NotificationsEvicted, 1);
                }

                _History.Add(notification);
                TrimHistory();
            }

            Changed?.Invoke();
            return notification;
        }

        /// <summary>
        /// Returns the notifications that have not expired or been dismissed at the supplied time, newest
        /// first, and prunes the others from the screen (they stay in <see cref="History"/>).
        /// </summary>
        /// <param name="nowMilliseconds">The current time in milliseconds.</param>
        /// <returns>The active notifications. Never null.</returns>
        public IReadOnlyList<Notification> Active(long nowMilliseconds)
        {
            lock (_Sync)
            {
                for (int i = _Items.Count - 1; i >= 0; i--)
                {
                    if (_Items[i].IsExpired(nowMilliseconds))
                        _Items.RemoveAt(i);
                }

                List<Notification> result = new List<Notification>(_Items.Count);
                for (int i = _Items.Count - 1; i >= 0; i--)
                    result.Add(_Items[i]);

                return result;
            }
        }

        /// <summary>
        /// Dismisses a notification's toast and marks it read. It stays in <see cref="History"/>.
        /// </summary>
        /// <param name="notification">The notification. Must not be null.</param>
        /// <returns><c>true</c> when the toast was showing; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="notification"/> is null.</exception>
        public bool Dismiss(Notification notification)
        {
            if (notification == null)
                throw new ArgumentNullException(nameof(notification));

            bool removed;
            lock (_Sync)
            {
                notification.IsDismissed = true;
                notification.IsRead = true;
                removed = _Items.Remove(notification);
            }

            Changed?.Invoke();
            return removed;
        }

        /// <summary>
        /// Dismisses the newest showing toast, for a keyboard shortcut.
        /// </summary>
        /// <returns><c>true</c> when a toast was dismissed; otherwise <c>false</c>.</returns>
        public bool DismissLatest()
        {
            Notification? latest;
            lock (_Sync)
                latest = _Items.Count > 0 ? _Items[_Items.Count - 1] : null;

            return latest != null && Dismiss(latest);
        }

        /// <summary>
        /// Dismisses every showing toast. History is kept.
        /// </summary>
        public void DismissAll()
        {
            lock (_Sync)
            {
                for (int i = 0; i < _Items.Count; i++)
                {
                    _Items[i].IsDismissed = true;
                    _Items[i].IsRead = true;
                }

                _Items.Clear();
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// Runs one of a notification's actions and dismisses its toast.
        /// </summary>
        /// <param name="notification">The notification. Must not be null.</param>
        /// <param name="actionIndex">The zero-based action index. Must be within the notification's actions.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="notification"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="actionIndex"/> is out of range.</exception>
        public void InvokeAction(Notification notification, int actionIndex)
        {
            if (notification == null)
                throw new ArgumentNullException(nameof(notification));
            if (actionIndex < 0 || actionIndex >= notification.Actions.Count)
                throw new ArgumentOutOfRangeException(nameof(actionIndex), actionIndex, "Action index is out of range.");

            Dismiss(notification);
            notification.Actions[actionIndex].Callback();
        }

        /// <summary>
        /// Marks every notification in history read.
        /// </summary>
        public void MarkAllRead()
        {
            lock (_Sync)
            {
                for (int i = 0; i < _History.Count; i++)
                    _History[i].IsRead = true;
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// Removes a notification from history (and its toast from the screen).
        /// </summary>
        /// <param name="notification">The notification. Must not be null.</param>
        /// <returns><c>true</c> when it was in history; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="notification"/> is null.</exception>
        public bool Remove(Notification notification)
        {
            if (notification == null)
                throw new ArgumentNullException(nameof(notification));

            bool removed;
            lock (_Sync)
            {
                notification.IsDismissed = true;
                _Items.Remove(notification);
                removed = _History.Remove(notification);
            }

            Changed?.Invoke();
            return removed;
        }

        /// <summary>
        /// Clears the history. Showing toasts are not affected.
        /// </summary>
        public void ClearHistory()
        {
            lock (_Sync)
                _History.Clear();

            Changed?.Invoke();
        }

        /// <summary>
        /// Applies a theme: severity styles from <see cref="Theme.Info"/>, <see cref="Theme.Success"/>,
        /// <see cref="Theme.Warning"/>, and <see cref="Theme.Error"/>, and the toast background from the
        /// <see cref="Theme.ToastRole"/> style when registered, otherwise <see cref="Theme.Text"/>.
        /// </summary>
        /// <param name="theme">The theme. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
        public void ApplyTheme(Theme theme)
        {
            if (theme == null)
                throw new ArgumentNullException(nameof(theme));

            lock (_Sync)
            {
                _BackgroundStyle = theme.Resolve(Theme.ToastRole, theme.Text);
                _InfoStyle = theme.Info;
                _SuccessStyle = theme.Success;
                _WarningStyle = theme.Warning;
                _ErrorStyle = theme.Error;
            }
        }

        /// <summary>
        /// Renders active notifications into the top-right corner of the surface, stacking downward.
        /// Each line is framed with exactly one leading and one trailing space.
        /// </summary>
        /// <param name="surface">The screen surface. Must not be null.</param>
        /// <param name="nowMilliseconds">The current time in milliseconds.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="surface"/> is null.</exception>
        public void Render(ISurface surface, long nowMilliseconds)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            IReadOnlyList<Notification> active = Active(nowMilliseconds);
            lock (_Sync)
            {
                _Hits.Clear();
                int width = Math.Min(_ToastWidth, surface.Size.Width - 2);
                if (width <= 0)
                    return;

                int x = surface.Size.Width - width - 1;
                int row = 0;
                for (int i = 0; i < active.Count && row < surface.Size.Height; i++)
                    row = RenderToast(surface, active[i], x, row, width);
            }
        }

        /// <summary>
        /// Handles a mouse press on a rendered toast (screen coordinates): runs the action under the
        /// pointer, dismisses on the dismiss marker, or, with <see cref="DismissOnClick"/>, dismisses on a
        /// click anywhere on the toast. Other events and clicks elsewhere are not consumed.
        /// </summary>
        /// <param name="mouse">The mouse event in screen coordinates. Must not be null.</param>
        /// <returns><c>true</c> when the click acted on a toast; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mouse"/> is null.</exception>
        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            if (mouse.Kind != MouseEventKind.Press || mouse.Button != MouseButton.Left)
                return false;

            Notification? target = null;
            int action = -1;
            bool dismiss = false;
            lock (_Sync)
            {
                for (int i = 0; i < _Hits.Count && target == null; i++)
                {
                    ToastHitRegion hit = _Hits[i];
                    if (!hit.Bounds.Contains(new Point(mouse.X, mouse.Y)))
                        continue;

                    for (int a = 0; a < hit.ActionRects.Count; a++)
                    {
                        if (hit.ActionRects[a].Contains(new Point(mouse.X, mouse.Y)))
                        {
                            target = hit.Notification;
                            action = a;
                        }
                    }

                    if (target == null && hit.DismissRect.Contains(new Point(mouse.X, mouse.Y)))
                    {
                        target = hit.Notification;
                        dismiss = true;
                    }

                    if (target == null && _DismissOnClick)
                    {
                        target = hit.Notification;
                        dismiss = true;
                    }
                }
            }

            if (target == null)
                return false;

            if (action >= 0)
                InvokeAction(target, action);
            else if (dismiss)
                Dismiss(target);

            return true;
        }

        private int RenderToast(ISurface surface, Notification item, int x, int row, int width)
        {
            CellStyle style = SeverityStyle(item.Severity).Over(_BackgroundStyle);
            int inner = Math.Max(0, width - 2);
            int top = row;
            bool simple = _MaxToastLines == 1 && item.Title == null && item.Actions.Count == 0 && !_ShowDismissButton;
            if (simple)
            {
                // The original single-line toast, byte for byte.
                surface.Fill(new Rect(x, row, width, 1), Cell.Blank(_BackgroundStyle));
                string label = " " + TextFit.Ellipsize(item.Text.Trim(), inner) + " ";
                surface.DrawText(x, row, label, style);
                _Hits.Add(new ToastHitRegion(item, new Rect(x, row, width, 1)));
                return row + 1;
            }

            int maxRow = surface.Size.Height;
            int textWidth = _ShowDismissButton ? Math.Max(1, inner - 2) : inner;
            Rect dismissRect = default;

            if (item.Title != null && row < maxRow)
            {
                surface.Fill(new Rect(x, row, width, 1), Cell.Blank(_BackgroundStyle));
                surface.DrawText(x + 1, row, TextFit.Ellipsize(item.Title, textWidth), style.WithAttribute(CellAttributes.Bold, true));
                if (_ShowDismissButton)
                    dismissRect = DrawDismiss(surface, x, row, width, style);
                row++;
            }

            IReadOnlyList<StyledText> wrapped = TextWrapper.Wrap(Text.From(item.Text.Trim()), Math.Max(1, textWidth));
            int lines = Math.Min(_MaxToastLines, wrapped.Count);
            for (int l = 0; l < lines && row < maxRow; l++)
            {
                string line = wrapped[l].ToPlainString();
                if (l == lines - 1 && wrapped.Count > lines)
                    line = TextFit.Ellipsize(line + " " + TextFit.DefaultEllipsis, textWidth);

                surface.Fill(new Rect(x, row, width, 1), Cell.Blank(_BackgroundStyle));
                surface.DrawText(x + 1, row, TextFit.Truncate(line, textWidth), style);
                if (_ShowDismissButton && item.Title == null && l == 0)
                    dismissRect = DrawDismiss(surface, x, row, width, style);
                row++;
            }

            ToastHitRegion hit = new ToastHitRegion(item, new Rect(x, top, width, Math.Max(1, row - top)));
            if (item.Actions.Count > 0 && row < maxRow)
            {
                surface.Fill(new Rect(x, row, width, 1), Cell.Blank(_BackgroundStyle));
                int cursor = x + 1;
                for (int a = 0; a < item.Actions.Count; a++)
                {
                    string label = "[" + item.Actions[a].Label + "]";
                    int labelWidth = TextFit.Width(label);
                    if (cursor + labelWidth > x + width - 1)
                        break;

                    surface.DrawText(cursor, row, label, MarkdownStyles.Overlay(style, _ActionStyle));
                    hit.ActionRects.Add(new Rect(cursor, row, labelWidth, 1));
                    cursor += labelWidth + 1;
                }

                row++;
                hit = CopyWithBounds(hit, new Rect(x, top, width, row - top));
            }

            hit.DismissRect = dismissRect;
            _Hits.Add(hit);
            return row;
        }

        private static ToastHitRegion CopyWithBounds(ToastHitRegion source, Rect bounds)
        {
            ToastHitRegion copy = new ToastHitRegion(source.Notification, bounds);
            copy.ActionRects.AddRange(source.ActionRects);
            copy.DismissRect = source.DismissRect;
            return copy;
        }

        private static Rect DrawDismiss(ISurface surface, int x, int row, int width, CellStyle style)
        {
            int column = x + width - 2;
            surface.DrawText(column, row, "x", style.WithAttribute(CellAttributes.Bold, true));
            return new Rect(column, row, 1, 1);
        }

        private CellStyle SeverityStyle(NotificationSeverity severity)
        {
            switch (severity)
            {
                case NotificationSeverity.Success:
                    return _SuccessStyle;
                case NotificationSeverity.Warning:
                    return _WarningStyle;
                case NotificationSeverity.Error:
                    return _ErrorStyle;
                default:
                    return _InfoStyle;
            }
        }

        private void TrimHistory()
        {
            while (_History.Count > _HistoryLimit)
                _History.RemoveAt(0);
        }

        private static string SeverityName(NotificationSeverity severity)
        {
            switch (severity)
            {
                case NotificationSeverity.Info:
                    return "info";
                case NotificationSeverity.Success:
                    return "success";
                case NotificationSeverity.Warning:
                    return "warning";
                case NotificationSeverity.Error:
                    return "error";
                default:
                    return "other";
            }
        }
    }
}

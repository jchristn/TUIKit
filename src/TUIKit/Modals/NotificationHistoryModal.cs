namespace TUIKit.Modals
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Theming;
    using TUIKit.Unicode;

    /// <summary>
    /// The notification center as a modal: the <see cref="NotificationCenter.History"/>, newest first,
    /// with severity, title, text, and age. Up/Down move; Enter runs the selected notification's first
    /// action; <c>d</c> or Delete removes it from history; <c>a</c> marks everything read; <c>c</c>
    /// clears the history; Escape closes. Opening the modal marks every notification read when
    /// <see cref="MarkReadOnOpen"/> is set. Completes with null.
    /// </summary>
    /// <remarks>Not thread-safe: use it from the UI loop.</remarks>
    public sealed class NotificationHistoryModal : DialogModal, IThemeable
    {
        private readonly NotificationCenter _Center;
        private readonly Func<long> _Now;
        private List<Notification> _Items = new List<Notification>();
        private int _Selected;
        private int _Top;
        private int _VisibleRows = 14;
        private bool _Opened;

        /// <summary>
        /// Gets or sets a value indicating whether opening the modal marks every notification read.
        /// Defaults to true.
        /// </summary>
        public bool MarkReadOnOpen { get; set; } = true;

        /// <summary>
        /// Gets or sets the highlighted row style. Defaults to black on cyan (palette 6).
        /// </summary>
        public CellStyle HighlightStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromRgb(0, 0, 0)).WithBackground(Color.FromPalette(6));

        /// <summary>
        /// Gets the selected notification, or null when the history is empty.
        /// </summary>
        public Notification? Selected
        {
            get { return _Selected >= 0 && _Selected < _Items.Count ? _Items[_Selected] : null; }
        }

        /// <summary>
        /// Gets the number of notifications listed.
        /// </summary>
        public int Count
        {
            get { return _Items.Count; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NotificationHistoryModal"/> class.
        /// </summary>
        /// <param name="center">The notification center. Must not be null.</param>
        /// <param name="now">A clock returning the current time in milliseconds (for ages), or null to omit ages.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="center"/> is null.</exception>
        public NotificationHistoryModal(NotificationCenter center, Func<long>? now = null)
        {
            _Center = center ?? throw new ArgumentNullException(nameof(center));
            _Now = now ?? (() => long.MinValue);
            Title = "Notifications";
            FooterHint = " Enter action  d delete  a read all  c clear  Esc close ";
            MinContentWidth = 40;
            MaxContentWidth = 90;
            Reload();
        }

        /// <summary>
        /// Applies a theme.
        /// </summary>
        /// <param name="theme">The theme. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
        public void ApplyTheme(Theme theme)
        {
            if (theme == null)
                throw new ArgumentNullException(nameof(theme));

            BorderStyleColor = theme.Accent;
            BackgroundStyle = theme.Text;
            HighlightStyle = theme.Selection;
        }

        /// <inheritdoc/>
        public override bool HandleKey(KeyEvent key)
        {
            switch (key.Code)
            {
                case KeyCode.Escape:
                    RequestClose(null);
                    return true;
                case KeyCode.Up:
                    _Selected = Math.Max(0, _Selected - 1);
                    return true;
                case KeyCode.Down:
                    _Selected = Math.Min(Math.Max(0, _Items.Count - 1), _Selected + 1);
                    return true;
                case KeyCode.Enter:
                    Notification? selected = Selected;
                    if (selected != null && selected.Actions.Count > 0)
                    {
                        Close(null);
                        _Center.InvokeAction(selected, 0);
                    }

                    return true;
                case KeyCode.Delete:
                    RemoveSelected();
                    return true;
                case KeyCode.Character:
                    if (key.Rune == 'd')
                        RemoveSelected();
                    else if (key.Rune == 'a')
                        _Center.MarkAllRead();
                    else if (key.Rune == 'c')
                    {
                        _Center.ClearHistory();
                        Reload();
                    }

                    return true;
                default:
                    return true;
            }
        }

        /// <inheritdoc/>
        protected override int MeasureContentWidth(int availableWidth)
        {
            return Math.Min(availableWidth, 70);
        }

        /// <inheritdoc/>
        protected override int MeasureContentHeight(int contentWidth)
        {
            return _VisibleRows;
        }

        /// <inheritdoc/>
        protected override void RenderContent(ISurface content)
        {
            if (!_Opened)
            {
                _Opened = true;
                if (MarkReadOnOpen)
                    _Center.MarkAllRead();
            }

            int width = content.Size.Width;
            int rows = content.Size.Height;
            if (_Items.Count == 0)
            {
                content.DrawText(0, 0, TextFit.Ellipsize("No notifications.", width), BackgroundStyle.WithAttribute(CellAttributes.Dim, true));
                return;
            }

            if (_Selected < _Top)
                _Top = _Selected;
            else if (_Selected >= _Top + rows)
                _Top = _Selected - rows + 1;

            long now = _Now();
            for (int r = 0; r < rows && _Top + r < _Items.Count; r++)
            {
                Notification item = _Items[_Top + r];
                bool selected = _Top + r == _Selected;
                CellStyle style = selected ? HighlightStyle : BackgroundStyle;
                content.Fill(new Rect(0, r, width, 1), Cell.Blank(style));
                string severity = "[" + SeverityLabel(item.Severity) + "] ";
                string age = now == long.MinValue ? string.Empty : " " + Age(now - item.CreatedAtMilliseconds);
                string text = (item.Title != null ? item.Title + ": " : string.Empty) + item.Text;
                if (item.Actions.Count > 0)
                    text += "  (" + item.Actions[0].Label + ")";
                int ageWidth = TextFit.Width(age);
                content.DrawText(0, r, severity, style.WithAttribute(CellAttributes.Bold, !item.IsRead));
                int x = TextFit.Width(severity);
                content.DrawText(x, r, TextFit.Ellipsize(text.Replace('\n', ' '), Math.Max(0, width - x - ageWidth)), style);
                if (ageWidth > 0 && ageWidth < width)
                    content.DrawText(width - ageWidth, r, age, style.WithAttribute(CellAttributes.Dim, !selected));
            }
        }

        private void RemoveSelected()
        {
            Notification? selected = Selected;
            if (selected == null)
                return;

            _Center.Remove(selected);
            Reload();
        }

        private void Reload()
        {
            _Items = new List<Notification>(_Center.History);
            _Selected = Math.Min(_Selected, Math.Max(0, _Items.Count - 1));
        }

        private static string SeverityLabel(NotificationSeverity severity)
        {
            switch (severity)
            {
                case NotificationSeverity.Success:
                    return "ok";
                case NotificationSeverity.Warning:
                    return "warn";
                case NotificationSeverity.Error:
                    return "error";
                default:
                    return "info";
            }
        }

        private static string Age(long milliseconds)
        {
            long seconds = Math.Max(0, milliseconds / 1000);
            if (seconds < 60)
                return seconds.ToString(CultureInfo.InvariantCulture) + "s";
            if (seconds < 3600)
                return (seconds / 60).ToString(CultureInfo.InvariantCulture) + "m";
            return (seconds / 3600).ToString(CultureInfo.InvariantCulture) + "h";
        }
    }
}

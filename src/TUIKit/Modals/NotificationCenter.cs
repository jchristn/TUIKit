namespace TUIKit.Modals
{
    using System;
    using System.Collections.Generic;
    using TUIKit;

    /// <summary>
    /// Collects and renders transient notifications (toasts). Toasts stack in the top-right corner,
    /// newest first, expire on their timeout, and never take focus. A configurable maximum caps how
    /// many are shown at once; older toasts are dropped when the cap is exceeded.
    /// </summary>
    /// <remarks>All members are thread-safe.</remarks>
    public sealed class NotificationCenter
    {
        private readonly object _Sync = new object();
        private readonly List<Notification> _Items = new List<Notification>();
        private int _MaxConcurrent = 5;
        private int _DefaultTimeoutMilliseconds = 4000;

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
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            lock (_Sync)
            {
                int timeout = timeoutMilliseconds ?? _DefaultTimeoutMilliseconds;
                Notification notification = new Notification(text, severity, nowMilliseconds, timeout);
                _Items.Add(notification);

                while (_Items.Count > _MaxConcurrent)
                    _Items.RemoveAt(0);

                return notification;
            }
        }

        /// <summary>
        /// Returns the notifications that have not expired at the supplied time, newest first, and
        /// prunes expired ones.
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
        /// Renders active notifications into the top-right corner of the surface, stacking downward.
        /// Each message is framed with exactly one leading and one trailing space.
        /// </summary>
        /// <param name="surface">The screen surface. Must not be null.</param>
        /// <param name="nowMilliseconds">The current time in milliseconds.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="surface"/> is null.</exception>
        public void Render(ISurface surface, long nowMilliseconds)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            IReadOnlyList<Notification> active = Active(nowMilliseconds);
            int width = Math.Min(40, surface.Size.Width - 2);
            if (width <= 0)
                return;

            int row = 0;
            for (int i = 0; i < active.Count && row < surface.Size.Height; i++)
            {
                Notification item = active[i];
                CellStyle style = CellStyle.Default.WithForeground(SeverityColor(item.Severity));
                int x = surface.Size.Width - width - 1;
                surface.Fill(new Rect(x, row, width, 1), Cell.Blank(CellStyle.Default));

                // Frame the message with exactly one leading and one trailing space.
                string label = " " + Truncate(item.Text, Math.Max(0, width - 2)) + " ";
                surface.DrawText(x, row, label, style);
                row++;
            }
        }

        private static Color SeverityColor(NotificationSeverity severity)
        {
            switch (severity)
            {
                case NotificationSeverity.Success:
                    return Color.FromPalette(2);
                case NotificationSeverity.Warning:
                    return Color.FromPalette(3);
                case NotificationSeverity.Error:
                    return Color.FromPalette(1);
                default:
                    return Color.FromPalette(6);
            }
        }

        private static string Truncate(string text, int width)
        {
            if (text.Length <= width)
                return text;

            return width <= 1 ? text.Substring(0, width) : text.Substring(0, width - 1) + "…";
        }
    }
}

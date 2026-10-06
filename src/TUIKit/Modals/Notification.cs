namespace TUIKit.Modals
{
    using System;
    using System.Threading;

    /// <summary>
    /// A transient notification (toast). Notifications never take focus; they appear, optionally time
    /// out, and can be dismissed. Timing is supplied by the caller as millisecond timestamps so the
    /// lifecycle is deterministic to test. A notification may carry a title and action buttons; once
    /// added to a <see cref="NotificationCenter"/> it stays in the center's history after its toast
    /// expires or is dismissed.
    /// </summary>
    /// <remarks>The read, dismissed, and identifier state is updated by the owning center under its lock.</remarks>
    public sealed class Notification
    {
        private static readonly NotificationAction[] _NoActions = new NotificationAction[0];
        private volatile bool _Dismissed;
        private volatile bool _Read;
        private int _RepeatCount = 1;
        private long _LastRaisedAtMilliseconds;
        private volatile string _Text;
        private volatile string? _Title;
        private volatile System.Collections.Generic.IReadOnlyList<NotificationAction> _Actions;
        private int _Severity;

        /// <summary>
        /// Gets the identifier assigned by the <see cref="NotificationCenter"/> that added the notification,
        /// increasing with each addition; zero before it is added.
        /// </summary>
        public long Id { get; internal set; }

        /// <summary>
        /// Gets the optional title shown in bold above the text, or null.
        /// </summary>
        public string? Title
        {
            get { return _Title; }
        }

        /// <summary>
        /// Gets the action buttons. Never null; empty when the notification has no actions.
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<NotificationAction> Actions
        {
            get { return _Actions; }
        }

        /// <summary>
        /// Gets the coalesce key the notification was raised with (see
        /// <see cref="NotificationOptions.CoalesceKey"/>), or null when it coalesces by content.
        /// </summary>
        public string? CoalesceKey { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether the toast was dismissed (by the user, an action, or the
        /// application). A dismissed notification is no longer shown as a toast but stays in history.
        /// </summary>
        public bool IsDismissed
        {
            get { return _Dismissed; }
            internal set { _Dismissed = value; }
        }

        /// <summary>
        /// Gets a value indicating whether the notification was read (dismissed, acted on, or marked read
        /// in the center).
        /// </summary>
        public bool IsRead
        {
            get { return _Read; }
            internal set { _Read = value; }
        }
        /// <summary>
        /// Gets the notification text. Never null.
        /// </summary>
        public string Text
        {
            get { return _Text; }
        }

        /// <summary>
        /// Gets the severity.
        /// </summary>
        public NotificationSeverity Severity
        {
            get { return (NotificationSeverity)Volatile.Read(ref _Severity); }
        }

        /// <summary>
        /// Gets the creation timestamp in milliseconds.
        /// </summary>
        public long CreatedAtMilliseconds { get; }

        /// <summary>
        /// Gets the timeout in milliseconds, or zero for a sticky notification that never expires on
        /// its own.
        /// </summary>
        public int TimeoutMilliseconds { get; }

        /// <summary>
        /// Gets how many times this notification has been raised. Starts at 1; when
        /// <see cref="NotificationCenter.CoalesceRepeats"/> is on, each identical raise while this
        /// notification is still showing increments it instead of adding a duplicate toast. Thread-safe.
        /// </summary>
        public int RepeatCount
        {
            get { return Volatile.Read(ref _RepeatCount); }
        }

        /// <summary>
        /// Gets the clock time, in milliseconds, of the most recent raise. Equals
        /// <see cref="CreatedAtMilliseconds"/> until a repeat is coalesced into this notification, which
        /// moves it forward and restarts the timeout. Expiry is measured from this value. Thread-safe.
        /// </summary>
        public long LastRaisedAtMilliseconds
        {
            get { return Interlocked.Read(ref _LastRaisedAtMilliseconds); }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Notification"/> class.
        /// </summary>
        /// <param name="text">The text. Must not be null.</param>
        /// <param name="severity">The severity.</param>
        /// <param name="createdAtMilliseconds">The creation timestamp.</param>
        /// <param name="timeoutMilliseconds">The timeout, or zero for sticky. Must be zero or greater.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the timeout is negative.</exception>
        public Notification(string text, NotificationSeverity severity, long createdAtMilliseconds, int timeoutMilliseconds)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));
            if (timeoutMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds), timeoutMilliseconds, "Timeout must be zero or greater.");

            _Text = text;
            _Severity = (int)severity;
            CreatedAtMilliseconds = createdAtMilliseconds;
            _LastRaisedAtMilliseconds = createdAtMilliseconds;
            TimeoutMilliseconds = timeoutMilliseconds;
            _Actions = _NoActions;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Notification"/> class with a title and actions.
        /// </summary>
        /// <param name="text">The text. Must not be null.</param>
        /// <param name="severity">The severity.</param>
        /// <param name="createdAtMilliseconds">The creation timestamp in milliseconds.</param>
        /// <param name="timeoutMilliseconds">The timeout in milliseconds; zero means sticky (no auto-expiry).</param>
        /// <param name="title">An optional title, or null.</param>
        /// <param name="actions">Optional action buttons, or null. Null entries are not allowed.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> or an action is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeoutMilliseconds"/> is negative.</exception>
        public Notification(string text, NotificationSeverity severity, long createdAtMilliseconds, int timeoutMilliseconds, string? title, System.Collections.Generic.IEnumerable<NotificationAction>? actions)
            : this(text, severity, createdAtMilliseconds, timeoutMilliseconds)
        {
            _Title = string.IsNullOrEmpty(title) ? null : title;
            if (actions != null)
            {
                System.Collections.Generic.List<NotificationAction> copy = new System.Collections.Generic.List<NotificationAction>();
                foreach (NotificationAction action in actions)
                {
                    if (action == null)
                        throw new ArgumentNullException(nameof(actions), "Actions must not contain null.");
                    copy.Add(action);
                }

                _Actions = copy;
            }
        }

        /// <summary>
        /// Determines whether the notification has expired at the supplied time.
        /// </summary>
        /// <param name="nowMilliseconds">The current time in milliseconds.</param>
        /// <returns><c>true</c> when the toast was dismissed or its timeout has elapsed since
        /// <see cref="LastRaisedAtMilliseconds"/>; otherwise <c>false</c>.</returns>
        public bool IsExpired(long nowMilliseconds)
        {
            if (_Dismissed)
                return true;

            if (TimeoutMilliseconds <= 0)
                return false;

            return nowMilliseconds - LastRaisedAtMilliseconds >= TimeoutMilliseconds;
        }

        // A coalesced raise brings the newest content (and callbacks) to the toast already on screen.
        // Called by the owning center under its lock.
        internal void ReplaceContent(string text, NotificationSeverity severity, string? title, System.Collections.Generic.IReadOnlyList<NotificationAction>? actions)
        {
            _Text = text;
            Volatile.Write(ref _Severity, (int)severity);
            _Title = string.IsNullOrEmpty(title) ? null : title;
            _Actions = actions == null || actions.Count == 0 ? _NoActions : new System.Collections.Generic.List<NotificationAction>(actions);
        }

        internal void RecordRepeat(long nowMilliseconds)
        {
            Interlocked.Increment(ref _RepeatCount);
            Interlocked.Exchange(ref _LastRaisedAtMilliseconds, nowMilliseconds);
            _Read = false;
        }
    }
}

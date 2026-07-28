namespace TUIKit.Modals
{
    using System;

    /// <summary>
    /// A transient notification (toast). Notifications never take focus; they appear, optionally time
    /// out, and can be dismissed. Timing is supplied by the caller as millisecond timestamps so the
    /// lifecycle is deterministic to test.
    /// </summary>
    public sealed class Notification
    {
        /// <summary>
        /// Gets the notification text. Never null.
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Gets the severity.
        /// </summary>
        public NotificationSeverity Severity { get; }

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

            Text = text;
            Severity = severity;
            CreatedAtMilliseconds = createdAtMilliseconds;
            TimeoutMilliseconds = timeoutMilliseconds;
        }

        /// <summary>
        /// Determines whether the notification has expired at the supplied time.
        /// </summary>
        /// <param name="nowMilliseconds">The current time in milliseconds.</param>
        /// <returns><c>true</c> when the timeout has elapsed; otherwise <c>false</c>.</returns>
        public bool IsExpired(long nowMilliseconds)
        {
            if (TimeoutMilliseconds <= 0)
                return false;

            return nowMilliseconds - CreatedAtMilliseconds >= TimeoutMilliseconds;
        }
    }
}

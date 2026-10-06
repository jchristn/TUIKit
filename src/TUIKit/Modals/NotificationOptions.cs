namespace TUIKit.Modals
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Optional inputs for raising a notification in one object (see
    /// <see cref="NotificationCenter.Add(string, NotificationSeverity, long, NotificationOptions)"/>): the
    /// timeout, title, and actions the other overloads take as parameters, plus a coalesce key. Not
    /// thread-safe; build it, pass it, and do not change it while a raise is in progress.
    /// </summary>
    public sealed class NotificationOptions
    {
        private int? _TimeoutMilliseconds;
        private IReadOnlyList<NotificationAction>? _Actions;

        /// <summary>
        /// Gets or sets a key that groups notifications regardless of their text, severity, title, and
        /// actions, or null to coalesce by content (see <see cref="NotificationCenter.CoalesceBy"/>). A
        /// raise whose key matches a toast still on screen updates that toast: it takes the newest text,
        /// severity, title, and actions, keeps its original creation time, and counts the repeat. A
        /// progress message ("Indexing 3 of 10", "Indexing 4 of 10") is the typical use. Empty is treated
        /// as null. Defaults to null.
        /// </summary>
        public string? CoalesceKey { get; set; }

        /// <summary>
        /// Gets or sets the timeout in milliseconds, or null for <see cref="NotificationCenter.DefaultTimeoutMilliseconds"/>.
        /// Zero makes the toast sticky until dismissed. Must be zero or greater. Defaults to null.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a negative value.</exception>
        public int? TimeoutMilliseconds
        {
            get { return _TimeoutMilliseconds; }
            set
            {
                if (value.HasValue && value.Value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), value.Value, "Timeout must be zero or greater.");

                _TimeoutMilliseconds = value;
            }
        }

        /// <summary>
        /// Gets or sets the optional title shown in bold above the text, or null. Defaults to null.
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Gets or sets the action buttons, or null for none. Must not contain null entries (checked when
        /// the notification is raised). Defaults to null.
        /// </summary>
        public IReadOnlyList<NotificationAction>? Actions
        {
            get { return _Actions; }
            set { _Actions = value; }
        }
    }
}

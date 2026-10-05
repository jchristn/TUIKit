namespace TUIKit.Modals
{
    using System;

    /// <summary>
    /// An action button on a <see cref="Notification"/>: a short label and the callback run when the user
    /// clicks it on the toast, picks it in the notification history, or the application calls
    /// <see cref="NotificationCenter.InvokeAction"/>. Running an action dismisses its toast. Instances are
    /// immutable and therefore thread-safe; the callback runs on the thread that invoked it.
    /// </summary>
    public sealed class NotificationAction
    {
        /// <summary>
        /// Gets the label shown on the toast. Never null or empty.
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// Gets the callback. Never null.
        /// </summary>
        public Action Callback { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="NotificationAction"/> class.
        /// </summary>
        /// <param name="label">The label. Must not be null or empty.</param>
        /// <param name="callback">The callback. Must not be null.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="label"/> is null or empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="callback"/> is null.</exception>
        public NotificationAction(string label, Action callback)
        {
            if (string.IsNullOrEmpty(label))
                throw new ArgumentException("Action label must not be null or empty.", nameof(label));

            Label = label;
            Callback = callback ?? throw new ArgumentNullException(nameof(callback));
        }
    }
}

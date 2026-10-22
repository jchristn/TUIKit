namespace TUIKit.Widgets
{
    using System;

    /// <summary>A mutable timeline entry. Update it only on the UI loop, then request a render.</summary>
    public sealed class ActivityEntry
    {
        private string _Text;
        private ActivityStatus _Status;

        /// <summary>Gets or sets the primary text. Must not be null; supports newlines.</summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string Text
        {
            get { return _Text; }
            set { _Text = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>Gets or sets optional detail rendered below the primary text. Defaults to null.</summary>
        public string? Detail { get; set; }

        /// <summary>Gets or sets the status. Defaults to Completed.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for an undefined status.</exception>
        public ActivityStatus Status
        {
            get { return _Status; }
            set
            {
                if (value < ActivityStatus.Pending || value > ActivityStatus.Failed)
                    throw new ArgumentOutOfRangeException(nameof(value));
                _Status = value;
            }
        }

        /// <summary>Creates a timeline entry.</summary>
        /// <param name="text">Primary text. Must not be null.</param>
        /// <param name="status">Initial status. Defaults to Completed.</param>
        /// <param name="detail">Optional detail. Defaults to null.</param>
        /// <exception cref="ArgumentNullException">Thrown when text is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for an undefined status.</exception>
        public ActivityEntry(string text, ActivityStatus status = ActivityStatus.Completed, string? detail = null)
        {
            _Text = text ?? throw new ArgumentNullException(nameof(text));
            Status = status;
            Detail = detail;
        }
    }
}

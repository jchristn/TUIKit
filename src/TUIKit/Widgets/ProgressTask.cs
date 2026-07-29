namespace TUIKit.Widgets
{
    using System;

    /// <summary>
    /// One tracked unit of work in a <see cref="MultiProgress"/> display: a label and a fractional
    /// completion in [0, 1]. Report progress with <see cref="Report"/>; the value is clamped, so
    /// callers can pass raw ratios without guarding the bounds.
    /// </summary>
    public sealed class ProgressTask
    {
        private double _Value;

        /// <summary>
        /// Gets or sets the task label.
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// Gets or sets the completion fraction, clamped to [0, 1].
        /// </summary>
        public double Value
        {
            get { return _Value; }
            set { _Value = value < 0 ? 0 : value > 1 ? 1 : value; }
        }

        /// <summary>
        /// Gets whether the task has reached completion.
        /// </summary>
        public bool IsComplete
        {
            get { return _Value >= 1.0; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProgressTask"/> class.
        /// </summary>
        /// <param name="label">The label. Must not be null.</param>
        /// <param name="value">The initial completion fraction.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="label"/> is null.</exception>
        public ProgressTask(string label, double value = 0)
        {
            Label = label ?? throw new ArgumentNullException(nameof(label));
            Value = value;
        }

        /// <summary>
        /// Reports a new completion fraction (clamped to [0, 1]).
        /// </summary>
        /// <param name="value">The new fraction.</param>
        public void Report(double value)
        {
            Value = value;
        }
    }
}

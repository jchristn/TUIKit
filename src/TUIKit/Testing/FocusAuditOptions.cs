namespace TUIKit.Testing
{
    using System;

    /// <summary>
    /// Controls what <see cref="FocusAudit"/> checks. Not thread-safe; configure before a run.
    /// </summary>
    public sealed class FocusAuditOptions
    {
        private int _MaxStops = 500;

        /// <summary>
        /// Gets or sets the most Tab presses before the audit gives up on returning to the start and
        /// reports <see cref="FocusAuditProblemKind.TraversalDidNotCycle"/>. Defaults to 500. Minimum 1,
        /// maximum 10000.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set outside 1 to 10000.</exception>
        public int MaxStops
        {
            get { return _MaxStops; }
            set
            {
                if (value < 1 || value > 10000)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Maximum stops must be between 1 and 10000.");

                _MaxStops = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether Shift+Tab must visit the stops in reverse Tab order.
        /// Defaults to true.
        /// </summary>
        public bool CheckSymmetry { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the region layout must stay the same at every stop.
        /// Defaults to true.
        /// </summary>
        public bool CheckLayoutStable { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether every stop must show focus visibly (see
        /// <see cref="FocusAuditProblemKind.NoFocusIndicator"/>). Defaults to true.
        /// </summary>
        public bool CheckFocusIndicator { get; set; } = true;
    }
}

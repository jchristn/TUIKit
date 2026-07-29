namespace TUIKit.Animation
{
    using System;

    /// <summary>
    /// Interpolates a scalar from a start to an end value over a fixed duration, shaped by an easing
    /// function. A tween holds no clock of its own; call <see cref="ValueAt"/> with the elapsed time,
    /// which makes it deterministic and trivial to unit-test. Combine with a <see cref="FrameTimer"/>
    /// or your own render loop to animate.
    /// </summary>
    public readonly struct Tween
    {
        private readonly Func<double, double> _Easing;

        /// <summary>Gets the start value (at elapsed 0).</summary>
        public double From { get; }

        /// <summary>Gets the end value (at or after the duration).</summary>
        public double To { get; }

        /// <summary>Gets the duration in milliseconds.</summary>
        public double DurationMs { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Tween"/> struct.
        /// </summary>
        /// <param name="from">The start value.</param>
        /// <param name="to">The end value.</param>
        /// <param name="durationMs">The duration in milliseconds. Must be positive.</param>
        /// <param name="easing">The easing function, or null for <see cref="Easing.Linear"/>.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="durationMs"/> is not positive.</exception>
        public Tween(double from, double to, double durationMs, Func<double, double>? easing = null)
        {
            if (durationMs <= 0)
                throw new ArgumentOutOfRangeException(nameof(durationMs));

            From = from;
            To = to;
            DurationMs = durationMs;
            _Easing = easing ?? Easing.Linear;
        }

        /// <summary>
        /// Gets the interpolated value at the given elapsed time. Times outside the duration are
        /// clamped, so the result never overshoots <see cref="From"/> or <see cref="To"/>.
        /// </summary>
        /// <param name="elapsedMs">The elapsed time in milliseconds.</param>
        /// <returns>The eased, interpolated value.</returns>
        public double ValueAt(double elapsedMs)
        {
            double t = elapsedMs / DurationMs;
            Func<double, double> easing = _Easing ?? Easing.Linear;
            return From + (To - From) * easing(t);
        }

        /// <summary>
        /// Gets whether the tween has reached its end at the given elapsed time.
        /// </summary>
        /// <param name="elapsedMs">The elapsed time in milliseconds.</param>
        /// <returns><c>true</c> when the elapsed time meets or exceeds the duration.</returns>
        public bool IsComplete(double elapsedMs)
        {
            return elapsedMs >= DurationMs;
        }
    }
}

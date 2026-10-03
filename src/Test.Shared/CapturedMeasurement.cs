namespace Test.Shared
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One metric measurement observed by <see cref="TelemetryCapture"/>. Immutable and thread-safe.
    /// </summary>
    public sealed class CapturedMeasurement
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CapturedMeasurement"/> class.
        /// </summary>
        /// <param name="instrument">The instrument name. Must not be null.</param>
        /// <param name="value">The measured value, widened to double.</param>
        /// <param name="tags">The measurement tags. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public CapturedMeasurement(string instrument, double value, IReadOnlyDictionary<string, object?> tags)
        {
            Instrument = instrument ?? throw new ArgumentNullException(nameof(instrument));
            Value = value;
            Tags = tags ?? throw new ArgumentNullException(nameof(tags));
        }

        /// <summary>
        /// Gets the instrument name. Never null.
        /// </summary>
        public string Instrument { get; }

        /// <summary>
        /// Gets the measured value.
        /// </summary>
        public double Value { get; }

        /// <summary>
        /// Gets the measurement tags. Never null.
        /// </summary>
        public IReadOnlyDictionary<string, object?> Tags { get; }

        /// <summary>
        /// Returns whether the measurement carries a tag with the given key and string value.
        /// </summary>
        /// <param name="key">The tag key. Must not be null.</param>
        /// <param name="value">The expected value.</param>
        /// <returns><c>true</c> when the tag is present with that value.</returns>
        public bool Has(string key, string value)
        {
            return Tags.TryGetValue(key, out object? actual) && actual != null && string.Equals(actual.ToString(), value, StringComparison.Ordinal);
        }
    }
}

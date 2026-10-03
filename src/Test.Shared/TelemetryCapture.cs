namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Linq;
    using TUIKit.Diagnostics;

    /// <summary>
    /// An in-memory subscriber to the TUIKit meter and activity source used to prove telemetry is
    /// emitted. Dispose it to unsubscribe. Thread-safe.
    /// </summary>
    public sealed class TelemetryCapture : IDisposable
    {
        private readonly object _Sync = new object();
        private readonly List<CapturedMeasurement> _Measurements = new List<CapturedMeasurement>();
        private readonly List<Activity> _Activities = new List<Activity>();
        private readonly MeterListener _MeterListener;
        private readonly ActivityListener _ActivityListener;
        private bool _Disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="TelemetryCapture"/> class and subscribes to every
        /// TUIKit instrument and span. Also samples spans from <paramref name="extraSource"/> when given,
        /// so a test can create a parent context.
        /// </summary>
        /// <param name="extraSource">An additional activity source name to sample, or null.</param>
        public TelemetryCapture(string? extraSource = null)
        {
            _MeterListener = new MeterListener();
            _MeterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == TuiKitTelemetryNames.MeterName)
                    listener.EnableMeasurementEvents(instrument);
            };
            _MeterListener.SetMeasurementEventCallback<long>((instrument, value, tags, state) => Add(instrument, value, tags));
            _MeterListener.SetMeasurementEventCallback<int>((instrument, value, tags, state) => Add(instrument, value, tags));
            _MeterListener.SetMeasurementEventCallback<double>((instrument, value, tags, state) => Add(instrument, value, tags));
            _MeterListener.Start();

            _ActivityListener = new ActivityListener();
            _ActivityListener.ShouldListenTo = source => source.Name == TuiKitTelemetryNames.ActivitySourceName || (extraSource != null && source.Name == extraSource);
            _ActivityListener.Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded;
            _ActivityListener.ActivityStopped = activity =>
            {
                lock (_Sync)
                    _Activities.Add(activity);
            };
            ActivitySource.AddActivityListener(_ActivityListener);
        }

        /// <summary>
        /// Gets a snapshot of the measurements captured so far. Never null.
        /// </summary>
        public IReadOnlyList<CapturedMeasurement> Measurements
        {
            get { lock (_Sync) { return _Measurements.ToList(); } }
        }

        /// <summary>
        /// Gets a snapshot of the stopped activities captured so far. Never null.
        /// </summary>
        public IReadOnlyList<Activity> Activities
        {
            get { lock (_Sync) { return _Activities.ToList(); } }
        }

        /// <summary>
        /// Polls every observable instrument so gauges report into the capture.
        /// </summary>
        public void Observe()
        {
            _MeterListener.RecordObservableInstruments();
        }

        /// <summary>
        /// Returns the captured measurements for one instrument. Never null.
        /// </summary>
        /// <param name="instrument">The instrument name. Must not be null.</param>
        /// <returns>The matching measurements.</returns>
        public IReadOnlyList<CapturedMeasurement> Of(string instrument)
        {
            return Measurements.Where(m => m.Instrument == instrument).ToList();
        }

        /// <summary>
        /// Sums the captured values of one instrument whose tags match every given key/value pair.
        /// </summary>
        /// <param name="instrument">The instrument name. Must not be null.</param>
        /// <param name="tags">Alternating tag keys and values to match.</param>
        /// <returns>The sum of matching values.</returns>
        public double Sum(string instrument, params string[] tags)
        {
            return Matching(instrument, tags).Sum(m => m.Value);
        }

        /// <summary>
        /// Counts the captured measurements of one instrument whose tags match every given key/value pair.
        /// </summary>
        /// <param name="instrument">The instrument name. Must not be null.</param>
        /// <param name="tags">Alternating tag keys and values to match.</param>
        /// <returns>The number of matching measurements.</returns>
        public int Count(string instrument, params string[] tags)
        {
            return Matching(instrument, tags).Count();
        }

        /// <summary>
        /// Returns the stopped activities with the given operation name. Never null.
        /// </summary>
        /// <param name="name">The span name. Must not be null.</param>
        /// <returns>The matching activities.</returns>
        public IReadOnlyList<Activity> Spans(string name)
        {
            return Activities.Where(a => a.OperationName == name).ToList();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_Disposed)
                return;

            _Disposed = true;
            _MeterListener.Dispose();
            _ActivityListener.Dispose();
        }

        private IEnumerable<CapturedMeasurement> Matching(string instrument, string[] tags)
        {
            return Of(instrument).Where(m =>
            {
                for (int i = 0; i + 1 < tags.Length; i += 2)
                {
                    if (!m.Has(tags[i], tags[i + 1]))
                        return false;
                }

                return true;
            });
        }

        private void Add(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            Dictionary<string, object?> copy = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object?> tag in tags)
                copy[tag.Key] = tag.Value;

            lock (_Sync)
                _Measurements.Add(new CapturedMeasurement(instrument.Name, value, copy));
        }
    }
}

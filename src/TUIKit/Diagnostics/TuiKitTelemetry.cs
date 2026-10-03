namespace TUIKit.Diagnostics
{
    using System.Diagnostics;
    using System.Diagnostics.Metrics;

    /// <summary>
    /// TUIKit's telemetry entry point. The library emits metrics on a BCL <see cref="System.Diagnostics.Metrics.Meter"/>
    /// and traces on a BCL <see cref="System.Diagnostics.ActivitySource"/>, both named <c>TUIKit</c>
    /// (<see cref="TuiKitTelemetryNames.MeterName"/>, <see cref="TuiKitTelemetryNames.ActivitySourceName"/>).
    /// TUIKit takes no dependency on any exporter or SDK and opens no connections; a host subscribes to
    /// the two names (for example with Radiant's <c>settings.Sources.AddMeter("TUIKit")</c> and
    /// <c>AddActivitySource("TUIKit")</c>, or OpenTelemetry's <c>AddMeter</c>/<c>AddSource</c>). With
    /// nobody subscribed, every recording is a near-zero-cost no-op. All members are thread-safe.
    /// </summary>
    public static class TuiKitTelemetry
    {
        private static readonly Meter _Meter = new Meter(TuiKitTelemetryNames.MeterName, TuiKitLibrary.Version);
        private static readonly ActivitySource _ActivitySource = new ActivitySource(TuiKitTelemetryNames.ActivitySourceName, TuiKitLibrary.Version);
        private static volatile bool _TraceFrames;
        private static volatile bool _Enabled = true;

        /// <summary>
        /// Gets the meter every TUIKit metric is recorded on. Never null. The instance lives for the
        /// life of the process; do not dispose it.
        /// </summary>
        public static Meter Meter
        {
            get { return _Meter; }
        }

        /// <summary>
        /// Gets the activity source every TUIKit span is started from. Never null. The instance lives
        /// for the life of the process; do not dispose it.
        /// </summary>
        public static ActivitySource ActivitySource
        {
            get { return _ActivitySource; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether TUIKit emits telemetry at all. Defaults to true.
        /// When false, no metric is recorded and no span is started, even with a listener attached;
        /// observable gauges still report their current values. Emission with no listener is already
        /// effectively free, so leave this on unless a host must silence TUIKit specifically.
        /// </summary>
        public static bool Enabled
        {
            get { return _Enabled; }
            set { _Enabled = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether every rendered frame opens a <c>tuikit.frame</c> span
        /// with <c>stage:compose</c>, <c>stage:diff</c>, and <c>stage:write</c> children. Defaults to
        /// false, because an interactive loop renders up to <c>TargetFps</c> frames per second and would
        /// flood a trace backend; frame timing is always available from the
        /// <see cref="TuiKitTelemetryNames.FrameDuration"/> and
        /// <see cref="TuiKitTelemetryNames.FrameStageDuration"/> histograms. Turn it on while profiling
        /// a slow frame, ideally with a sampling trace provider.
        /// </summary>
        public static bool TraceFrames
        {
            get { return _TraceFrames; }
            set { _TraceFrames = value; }
        }
    }
}

namespace TUIKit.Diagnostics
{
    using System;

    /// <summary>
    /// Tracks recent frame durations and derives an average and effective frame rate. Feed it one
    /// measurement per rendered frame. All members are thread-safe.
    /// </summary>
    public sealed class FrameStats
    {
        private readonly object _Sync = new object();
        private readonly double[] _Samples;
        private int _Count;
        private int _Index;
        private double _LastMilliseconds;

        /// <summary>
        /// Initializes a new instance of the <see cref="FrameStats"/> class.
        /// </summary>
        /// <param name="window">The number of recent frames to average over. Must be greater than zero. Defaults to 60.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="window"/> is non-positive.</exception>
        public FrameStats(int window = 60)
        {
            if (window <= 0)
                throw new ArgumentOutOfRangeException(nameof(window), window, "Window must be greater than zero.");

            _Samples = new double[window];
        }

        /// <summary>
        /// Gets the duration of the most recent frame in milliseconds.
        /// </summary>
        public double LastMilliseconds
        {
            get { lock (_Sync) { return _LastMilliseconds; } }
        }

        /// <summary>
        /// Gets the total number of frames recorded.
        /// </summary>
        public long FrameCount { get; private set; }

        /// <summary>
        /// Records a frame duration.
        /// </summary>
        /// <param name="milliseconds">The frame duration in milliseconds. Must be zero or greater.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is negative.</exception>
        public void Record(double milliseconds)
        {
            if (milliseconds < 0.0)
                throw new ArgumentOutOfRangeException(nameof(milliseconds), milliseconds, "Duration must be zero or greater.");

            lock (_Sync)
            {
                _LastMilliseconds = milliseconds;
                _Samples[_Index] = milliseconds;
                _Index = (_Index + 1) % _Samples.Length;
                if (_Count < _Samples.Length)
                    _Count++;
                FrameCount++;
            }
        }

        /// <summary>
        /// Gets the average frame duration over the recent window in milliseconds.
        /// </summary>
        /// <returns>The average, or zero when no frames have been recorded.</returns>
        public double AverageMilliseconds()
        {
            lock (_Sync)
            {
                if (_Count == 0)
                    return 0.0;

                double total = 0.0;
                for (int i = 0; i < _Count; i++)
                    total += _Samples[i];

                return total / _Count;
            }
        }

        /// <summary>
        /// Gets the effective frames per second derived from the average frame duration.
        /// </summary>
        /// <returns>The effective FPS, or zero when unavailable.</returns>
        public double FramesPerSecond()
        {
            double average = AverageMilliseconds();
            return average > 0.0 ? 1000.0 / average : 0.0;
        }
    }
}

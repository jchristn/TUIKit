namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit;

    /// <summary>
    /// Bins a numeric series into equal-width buckets and draws bucket frequency as vertical columns,
    /// answering "how many samples fell where" (a latency or token-count distribution). It complements
    /// <see cref="BoxPlotChart"/>, which shows the five-number summary; the histogram shows the shape.
    /// </summary>
    /// <remarks>
    /// Columns use the eighth-block ramp (<c>▁▂▃▄▅▆▇█</c>) scaled to the tallest bucket. An empty series
    /// renders nothing. Non-finite samples are ignored when deriving the domain and clamped into the end
    /// buckets when counted, so a bad sample degrades instead of crashing. This type is not thread-safe.
    /// </remarks>
    public sealed class Histogram : IWidget
    {
        private static readonly string[] _Bars = { "▁", "▂", "▃", "▄", "▅", "▆", "▇", "█" };

        private readonly List<double> _Values = new List<double>();
        private int _BucketCount = 10;
        private bool _HasRange;
        private double _RangeMin;
        private double _RangeMax;

        /// <summary>
        /// Gets or sets the color of the columns. Defaults to palette green.
        /// </summary>
        public Color BarColor { get; set; } = Color.FromPalette(2);

        /// <summary>
        /// Gets or sets the number of buckets. Values below 1 are clamped to 1. Defaults to 10.
        /// </summary>
        public int BucketCount
        {
            get { return _BucketCount; }
            set { _BucketCount = value < 1 ? 1 : value; }
        }

        /// <summary>
        /// Gets or sets whether the per-bucket count is printed on the bottom row when each bucket column
        /// is wide enough to hold it. Defaults to false.
        /// </summary>
        public bool ShowCounts { get; set; }

        /// <summary>
        /// Gets the number of samples.
        /// </summary>
        public int Count
        {
            get { return _Values.Count; }
        }

        /// <summary>
        /// Replaces the series with the supplied values.
        /// </summary>
        /// <param name="values">The values. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="values"/> is null.</exception>
        public void SetValues(IEnumerable<double> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            _Values.Clear();
            _Values.AddRange(values);
        }

        /// <summary>
        /// Appends a value, trimming the series to at most the supplied capacity, so the histogram can
        /// back a live feed.
        /// </summary>
        /// <param name="value">The value to append.</param>
        /// <param name="capacity">The maximum series length. Must be greater than zero.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="capacity"/> is non-positive.</exception>
        public void Push(double value, int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be greater than zero.");

            _Values.Add(value);
            while (_Values.Count > capacity)
                _Values.RemoveAt(0);
        }

        /// <summary>
        /// Removes every sample.
        /// </summary>
        public void Clear()
        {
            _Values.Clear();
        }

        /// <summary>
        /// Fixes the bucket domain to the supplied range instead of deriving it from the data, so two
        /// series with different natural ranges bucket identically.
        /// </summary>
        /// <param name="min">The lower edge of the first bucket.</param>
        /// <param name="max">The upper edge of the last bucket.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="min"/> or <paramref name="max"/> is not finite, or when
        /// <paramref name="max"/> is not greater than <paramref name="min"/>.
        /// </exception>
        public void SetRange(double min, double max)
        {
            if (double.IsNaN(min) || double.IsInfinity(min))
                throw new ArgumentException("Range minimum must be finite.", nameof(min));
            if (double.IsNaN(max) || double.IsInfinity(max))
                throw new ArgumentException("Range maximum must be finite.", nameof(max));
            if (max <= min)
                throw new ArgumentException("Range maximum must be greater than the minimum.", nameof(max));

            _RangeMin = min;
            _RangeMax = max;
            _HasRange = true;
        }

        /// <summary>
        /// Computes the current per-bucket sample counts using the active domain (fixed via
        /// <see cref="SetRange"/> or derived from the data). Non-finite samples are clamped into the end
        /// buckets. The returned list always has <see cref="BucketCount"/> entries.
        /// </summary>
        /// <returns>The count of samples in each bucket, from the lowest to the highest. Never null.</returns>
        public IReadOnlyList<int> ComputeBucketCounts()
        {
            int[] counts = new int[_BucketCount];
            if (_Values.Count == 0)
                return counts;

            double min;
            double max;
            ResolveDomain(out min, out max);
            double span = max - min;

            for (int i = 0; i < _Values.Count; i++)
            {
                double value = _Values[i];
                int bucket;
                if (span <= 0)
                {
                    bucket = 0;
                }
                else
                {
                    double clamped = Sanitize(value, min, max);
                    bucket = (int)((clamped - min) / span * _BucketCount);
                    if (bucket < 0)
                        bucket = 0;
                    if (bucket > _BucketCount - 1)
                        bucket = _BucketCount - 1;
                }

                counts[bucket]++;
            }

            return counts;
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            if (_Values.Count == 0)
                return new Size(0, 0);

            return new Size(Math.Min(available.Width, _BucketCount), available.Height);
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            int width = surface.Size.Width;
            int height = surface.Size.Height;
            if (width <= 0 || height <= 0 || _Values.Count == 0)
                return;

            IReadOnlyList<int> counts = ComputeBucketCounts();
            int maxCount = 0;
            for (int i = 0; i < counts.Count; i++)
            {
                if (counts[i] > maxCount)
                    maxCount = counts[i];
            }

            if (maxCount == 0)
                return;

            int bucketWidth = Math.Max(1, width / _BucketCount);
            CellStyle style = CellStyle.Default.WithForeground(BarColor);

            for (int bucket = 0; bucket < _BucketCount; bucket++)
            {
                int x0 = bucket * bucketWidth;
                if (x0 >= width)
                    break;

                double fraction = (double)counts[bucket] / maxCount;
                int totalEighths = (int)Math.Round(fraction * height * 8, MidpointRounding.AwayFromZero);
                if (totalEighths <= 0)
                    continue;

                int full = totalEighths / 8;
                int remainder = totalEighths % 8;

                for (int cx = x0; cx < x0 + bucketWidth && cx < width; cx++)
                {
                    int y = height - 1;
                    for (int f = 0; f < full && y >= 0; f++, y--)
                        surface.Set(cx, y, Cell.Glyph("█", style, 1));

                    if (remainder > 0 && y >= 0)
                        surface.Set(cx, y, Cell.Glyph(_Bars[remainder - 1], style, 1));
                }
            }

            if (ShowCounts && bucketWidth >= 2)
                DrawCounts(surface, counts, bucketWidth, width, height, style);
        }

        private void DrawCounts(ISurface surface, IReadOnlyList<int> counts, int bucketWidth, int width, int height, CellStyle style)
        {
            int row = height - 1;
            for (int bucket = 0; bucket < _BucketCount; bucket++)
            {
                int x0 = bucket * bucketWidth;
                if (x0 >= width)
                    break;

                string text = counts[bucket].ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (text.Length > bucketWidth)
                    continue;

                int offset = (bucketWidth - text.Length) / 2;
                surface.DrawText(x0 + offset, row, text, style);
            }
        }

        private void ResolveDomain(out double min, out double max)
        {
            if (_HasRange)
            {
                min = _RangeMin;
                max = _RangeMax;
                return;
            }

            min = double.MaxValue;
            max = double.MinValue;
            for (int i = 0; i < _Values.Count; i++)
            {
                double value = _Values[i];
                if (double.IsNaN(value) || double.IsInfinity(value))
                    continue;
                if (value < min)
                    min = value;
                if (value > max)
                    max = value;
            }

            if (min == double.MaxValue || max == double.MinValue)
            {
                min = 0.0;
                max = 0.0;
            }
        }

        private static double Sanitize(double value, double lo, double hi)
        {
            if (double.IsNaN(value))
                return lo;
            if (double.IsPositiveInfinity(value))
                return hi;
            if (double.IsNegativeInfinity(value))
                return lo;
            if (value < lo)
                return lo;
            if (value > hi)
                return hi;

            return value;
        }
    }
}

namespace TUIKit.Widgets
{
    using System;

    /// <summary>
    /// An immutable five-number distribution summary for one row of a <see cref="BoxPlotChart"/>.
    /// The five values — <see cref="Min"/>, <see cref="Low"/>, <see cref="Mid"/>, <see cref="High"/>,
    /// and <see cref="Max"/> — are generic (deliberately not named for any one statistic) so a host can
    /// map them to quartiles, or to a min / average / p95 / p99 / max quintuple, as it sees fit. The
    /// widget stays presentation-only: it renders whatever five numbers it is handed and never computes
    /// them.
    /// </summary>
    /// <remarks>
    /// The five values are sorted ascending on construction, so an out-of-order caller still renders a
    /// sane box (whiskers outside the box, marker inside it). Instances are immutable and therefore safe
    /// to share across threads.
    /// </remarks>
    public sealed class BoxSummary
    {
        /// <summary>
        /// Gets the category label. Never null.
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// Gets the smallest value — the left whisker end.
        /// </summary>
        public double Min { get; }

        /// <summary>
        /// Gets the lower box edge.
        /// </summary>
        public double Low { get; }

        /// <summary>
        /// Gets the mid marker value, drawn as a distinct glyph inside the box.
        /// </summary>
        public double Mid { get; }

        /// <summary>
        /// Gets the upper box edge.
        /// </summary>
        public double High { get; }

        /// <summary>
        /// Gets the largest value — the right whisker end.
        /// </summary>
        public double Max { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BoxSummary"/> class. The five supplied values are
        /// sorted ascending before assignment, so an out-of-order caller still renders sanely.
        /// </summary>
        /// <param name="label">The category label. Must not be null.</param>
        /// <param name="min">One of the five distribution values (intended as the minimum / left whisker).</param>
        /// <param name="low">One of the five distribution values (intended as the lower box edge).</param>
        /// <param name="mid">One of the five distribution values (intended as the mid marker).</param>
        /// <param name="high">One of the five distribution values (intended as the upper box edge).</param>
        /// <param name="max">One of the five distribution values (intended as the maximum / right whisker).</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="label"/> is null.</exception>
        public BoxSummary(string label, double min, double low, double mid, double high, double max)
        {
            if (label == null)
                throw new ArgumentNullException(nameof(label));

            double[] values = new double[] { min, low, mid, high, max };
            Array.Sort(values);

            Label = label;
            Min = values[0];
            Low = values[1];
            Mid = values[2];
            High = values[3];
            Max = values[4];
        }
    }
}

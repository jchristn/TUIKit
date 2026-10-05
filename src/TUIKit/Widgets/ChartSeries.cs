namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit;

    /// <summary>
    /// A named data series for <see cref="LineChart"/> and <see cref="ColumnChart"/>: its values, its
    /// color, and an optional glyph used where the chart draws cells (so series stay distinguishable
    /// without color).
    /// </summary>
    /// <remarks>Not thread-safe: update it from the UI loop.</remarks>
    public sealed class ChartSeries
    {
        private readonly List<double> _Values = new List<double>();

        /// <summary>
        /// Gets the series name. Never null.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets or sets the series color.
        /// </summary>
        public Color Color { get; set; }

        /// <summary>
        /// Gets or sets the glyph used by cell-based charts (column segments, legend marks). Defaults to a
        /// full block (U+2588).
        /// </summary>
        public string Glyph { get; set; } = "\u2588";

        /// <summary>
        /// Gets the values. Never null.
        /// </summary>
        public IReadOnlyList<double> Values
        {
            get { return _Values; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ChartSeries"/> class.
        /// </summary>
        /// <param name="name">The name. Must not be null.</param>
        /// <param name="values">The values. Must not be null.</param>
        /// <param name="color">The color.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public ChartSeries(string name, IEnumerable<double> values, Color color)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            SetValues(values);
            Color = color;
        }

        /// <summary>
        /// Replaces the values.
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
    }
}

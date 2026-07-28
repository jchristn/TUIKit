namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit;

    /// <summary>
    /// A compact single-row chart that renders a numeric series as vertical bar glyphs, useful for
    /// live telemetry.
    /// </summary>
    public sealed class Sparkline : IWidget
    {
        private static readonly string[] _Bars = { "▁", "▂", "▃", "▄", "▅", "▆", "▇", "█" };
        private readonly List<double> _Values = new List<double>();

        /// <summary>
        /// Gets or sets the color of the chart. Defaults to palette cyan.
        /// </summary>
        public Color LineColor { get; set; } = Color.FromPalette(6);

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
        /// Appends a value, trimming the series to at most the supplied capacity.
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

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            return new Size(Math.Min(available.Width, _Values.Count), available.Height > 0 ? 1 : 0);
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            int width = surface.Size.Width;
            if (width <= 0 || _Values.Count == 0)
                return;

            double min = double.MaxValue;
            double max = double.MinValue;
            for (int i = 0; i < _Values.Count; i++)
            {
                if (_Values[i] < min)
                    min = _Values[i];
                if (_Values[i] > max)
                    max = _Values[i];
            }

            double range = max - min;
            CellStyle style = CellStyle.Default.WithForeground(LineColor);

            int start = Math.Max(0, _Values.Count - width);
            int column = 0;
            for (int i = start; i < _Values.Count && column < width; i++, column++)
            {
                double normalized = range > 0 ? (_Values[i] - min) / range : 0.5;
                int level = (int)Math.Round(normalized * (_Bars.Length - 1), MidpointRounding.AwayFromZero);
                if (level < 0)
                    level = 0;
                if (level >= _Bars.Length)
                    level = _Bars.Length - 1;

                surface.Set(column, 0, Cell.Glyph(_Bars[level], style, 1));
            }
        }
    }
}

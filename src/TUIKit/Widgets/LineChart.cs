namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit;

    /// <summary>
    /// A line chart that plots a numeric series onto a <see cref="BrailleCanvas"/>, auto-scaling the
    /// data to fill the region and connecting consecutive samples. Because it draws with Braille dots,
    /// a small region still shows a smooth curve. Rebind the data and re-render to animate.
    /// </summary>
    public sealed class LineChart : IWidget
    {
        private readonly List<double> _Values = new List<double>();

        /// <summary>
        /// Gets or sets the line color. Defaults to cyan.
        /// </summary>
        public Color Color { get; set; } = Color.FromPalette(6);

        /// <summary>
        /// Initializes a new instance of the <see cref="LineChart"/> class.
        /// </summary>
        /// <param name="values">The initial samples. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="values"/> is null.</exception>
        public LineChart(IEnumerable<double> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            SetValues(values);
        }

        /// <summary>
        /// Gets the number of samples.
        /// </summary>
        public int Count
        {
            get { return _Values.Count; }
        }

        /// <summary>
        /// Replaces the plotted samples.
        /// </summary>
        /// <param name="values">The new samples. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="values"/> is null.</exception>
        public void SetValues(IEnumerable<double> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            _Values.Clear();
            foreach (double value in values)
                _Values.Add(value);
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            return available;
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

            BrailleCanvas canvas = new BrailleCanvas(width, height);
            canvas.Color = Color;

            double min = _Values[0];
            double max = _Values[0];
            for (int i = 1; i < _Values.Count; i++)
            {
                if (_Values[i] < min)
                    min = _Values[i];
                if (_Values[i] > max)
                    max = _Values[i];
            }

            double range = max - min;
            int pixelWidth = canvas.PixelWidth;
            int pixelHeight = canvas.PixelHeight;
            int previousX = 0;
            int previousY = 0;

            for (int i = 0; i < _Values.Count; i++)
            {
                int px = _Values.Count == 1 ? 0 : (int)Math.Round(i * (double)(pixelWidth - 1) / (_Values.Count - 1));
                double normalized = range <= 0 ? 0.5 : (_Values[i] - min) / range;
                int py = pixelHeight - 1 - (int)Math.Round(normalized * (pixelHeight - 1));

                if (i == 0)
                    canvas.Set(px, py);
                else
                    canvas.Line(previousX, previousY, px, py);

                previousX = px;
                previousY = py;
            }

            canvas.Render(surface);
        }
    }
}

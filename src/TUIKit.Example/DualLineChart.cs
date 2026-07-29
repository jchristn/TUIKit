namespace TUIKit.Example
{
    using System;
    using TUIKit;
    using TUIKit.Widgets;

    /// <summary>
    /// Renders two independently-colored line-chart series into the same region by drawing one
    /// <see cref="LineChart"/> over another. Used by the tour to show two waves at once.
    /// </summary>
    internal sealed class DualLineChart : IWidget
    {
        private readonly LineChart _First;
        private readonly LineChart _Second;

        internal DualLineChart(double[] first, Color firstColor, double[] second, Color secondColor)
        {
            if (first == null)
                throw new ArgumentNullException(nameof(first));
            if (second == null)
                throw new ArgumentNullException(nameof(second));

            _First = new LineChart(first);
            _First.Color = firstColor;
            _Second = new LineChart(second);
            _Second.Color = secondColor;
        }

        public Size Measure(Size available)
        {
            return available;
        }

        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            _First.Render(surface);
            _Second.Render(surface);
        }
    }
}

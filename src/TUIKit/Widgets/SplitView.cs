namespace TUIKit.Widgets
{
    using System;
    using TUIKit;
    using TUIKit.Input;

    /// <summary>
    /// Divides its region between two child widgets along one axis, drawing an optional divider
    /// between them. The split position is a ratio in [<see cref="MinRatio"/>, <see cref="MaxRatio"/>]
    /// that the user can drag with the arrow keys (Left/Right when horizontal, Up/Down when vertical),
    /// giving resizable panes. Because a child may itself be a <see cref="SplitView"/>, arbitrarily
    /// nested layouts compose from this one widget.
    /// </summary>
    public sealed class SplitView : IWidget, IFocusable
    {
        private readonly IWidget _First;
        private readonly IWidget _Second;
        private double _Ratio;

        /// <summary>Gets or sets the split orientation.</summary>
        public SplitOrientation Orientation { get; set; }

        /// <summary>Gets or sets whether a divider line is drawn between the panes. Defaults to true.</summary>
        public bool ShowDivider { get; set; } = true;

        /// <summary>Gets or sets the minimum split ratio. Defaults to 0.1.</summary>
        public double MinRatio { get; set; } = 0.1;

        /// <summary>Gets or sets the maximum split ratio. Defaults to 0.9.</summary>
        public double MaxRatio { get; set; } = 0.9;

        /// <summary>Gets or sets the amount the ratio changes per resize key. Defaults to 0.05.</summary>
        public double ResizeStep { get; set; } = 0.05;

        /// <summary>
        /// Gets the current split ratio: the fraction of the area given to the first child.
        /// </summary>
        public double Ratio
        {
            get { return _Ratio; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SplitView"/> class.
        /// </summary>
        /// <param name="orientation">The split orientation.</param>
        /// <param name="first">The first (left or top) child. Must not be null.</param>
        /// <param name="second">The second (right or bottom) child. Must not be null.</param>
        /// <param name="ratio">The initial split ratio (0–1). Clamped to the min/max range.</param>
        /// <exception cref="ArgumentNullException">Thrown when a child is null.</exception>
        public SplitView(SplitOrientation orientation, IWidget first, IWidget second, double ratio = 0.5)
        {
            _First = first ?? throw new ArgumentNullException(nameof(first));
            _Second = second ?? throw new ArgumentNullException(nameof(second));
            Orientation = orientation;
            _Ratio = Clamp(ratio);
        }

        /// <summary>
        /// Resizes the split with the arrow keys aligned to the orientation.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns><c>true</c> when the key resized the split; otherwise <c>false</c>.</returns>
        public bool HandleKey(KeyEvent key)
        {
            if (Orientation == SplitOrientation.Horizontal)
            {
                if (key.Code == KeyCode.Left)
                {
                    _Ratio = Clamp(_Ratio - ResizeStep);
                    return true;
                }

                if (key.Code == KeyCode.Right)
                {
                    _Ratio = Clamp(_Ratio + ResizeStep);
                    return true;
                }
            }
            else
            {
                if (key.Code == KeyCode.Up)
                {
                    _Ratio = Clamp(_Ratio - ResizeStep);
                    return true;
                }

                if (key.Code == KeyCode.Down)
                {
                    _Ratio = Clamp(_Ratio + ResizeStep);
                    return true;
                }
            }

            return false;
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
            if (width <= 0 || height <= 0)
                return;

            int divider = ShowDivider ? 1 : 0;

            if (Orientation == SplitOrientation.Horizontal)
            {
                int usable = width - divider;
                if (usable < 2)
                {
                    _First.Render(surface);
                    return;
                }

                int firstWidth = Clamp(usable, (int)Math.Round(_Ratio * usable));
                int secondWidth = usable - firstWidth;

                _First.Render(new SurfaceView(surface, new Rect(0, 0, firstWidth, height)));
                if (divider > 0)
                    surface.Fill(new Rect(firstWidth, 0, 1, height), Cell.Glyph("│", CellStyle.Default.WithForeground(Color.FromPalette(8)), 1));
                _Second.Render(new SurfaceView(surface, new Rect(firstWidth + divider, 0, secondWidth, height)));
            }
            else
            {
                int usable = height - divider;
                if (usable < 2)
                {
                    _First.Render(surface);
                    return;
                }

                int firstHeight = Clamp(usable, (int)Math.Round(_Ratio * usable));
                int secondHeight = usable - firstHeight;

                _First.Render(new SurfaceView(surface, new Rect(0, 0, width, firstHeight)));
                if (divider > 0)
                    surface.Fill(new Rect(0, firstHeight, width, 1), Cell.Glyph("─", CellStyle.Default.WithForeground(Color.FromPalette(8)), 1));
                _Second.Render(new SurfaceView(surface, new Rect(0, firstHeight + divider, width, secondHeight)));
            }
        }

        private double Clamp(double ratio)
        {
            double min = MinRatio;
            double max = MaxRatio;
            if (ratio < min)
                return min;
            if (ratio > max)
                return max;

            return ratio;
        }

        private static int Clamp(int usable, int value)
        {
            if (value < 1)
                return 1;
            if (value > usable - 1)
                return usable - 1;

            return value;
        }
    }
}

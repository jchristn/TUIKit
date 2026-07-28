namespace TUIKit.Layout
{
    using System;

    /// <summary>
    /// Fluent builder for a <see cref="Region"/>. Set a horizontal and a vertical constraint, then
    /// call <see cref="Build"/>. Convenience methods cover the common anchoring patterns; for full
    /// control assign an <see cref="AxisConstraint"/> directly. Interior padding defaults to one cell
    /// on every side so content keeps clear of borders and debug outlines; call one of the
    /// <c>WithPadding</c> overloads to change it (for example vertical zero on a single-row bar).
    /// </summary>
    public sealed class RegionBuilder
    {
        private readonly string _Id;
        private AxisConstraint? _Horizontal;
        private AxisConstraint? _Vertical;
        private Padding _Padding = Padding.Uniform(1);

        /// <summary>
        /// Initializes a new instance of the <see cref="RegionBuilder"/> class.
        /// </summary>
        /// <param name="id">The region identifier. Must not be null or empty.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is null or empty.</exception>
        public RegionBuilder(string id)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Region id must not be null or empty.", nameof(id));

            _Id = id;
        }

        /// <summary>
        /// Sets the horizontal constraint directly.
        /// </summary>
        /// <param name="constraint">The horizontal constraint. Must not be null.</param>
        /// <returns>This builder.</returns>
        public RegionBuilder Horizontal(AxisConstraint constraint)
        {
            _Horizontal = constraint ?? throw new ArgumentNullException(nameof(constraint));
            return this;
        }

        /// <summary>
        /// Sets the vertical constraint directly.
        /// </summary>
        /// <param name="constraint">The vertical constraint. Must not be null.</param>
        /// <returns>This builder.</returns>
        public RegionBuilder Vertical(AxisConstraint constraint)
        {
            _Vertical = constraint ?? throw new ArgumentNullException(nameof(constraint));
            return this;
        }

        /// <summary>
        /// Anchors the region a fixed distance from the left edge with a fixed width.
        /// </summary>
        /// <param name="left">The offset from the left edge.</param>
        /// <param name="width">The width in cells.</param>
        /// <returns>This builder.</returns>
        public RegionBuilder LeftAnchored(int left, int width)
        {
            return Horizontal(AxisConstraint.Fixed(left, width));
        }

        /// <summary>
        /// Anchors the region a fixed distance from the right edge with a fixed width.
        /// </summary>
        /// <param name="right">The offset from the right edge.</param>
        /// <param name="width">The width in cells.</param>
        /// <returns>This builder.</returns>
        public RegionBuilder RightAnchored(int right, int width)
        {
            return Horizontal(AxisConstraint.FromEnd(right, width));
        }

        /// <summary>
        /// Stretches the region across the width between the supplied pads.
        /// </summary>
        /// <param name="leftPad">Cells reserved on the left. Defaults to 0.</param>
        /// <param name="rightPad">Cells reserved on the right. Defaults to 0.</param>
        /// <returns>This builder.</returns>
        public RegionBuilder FillWidth(int leftPad = 0, int rightPad = 0)
        {
            return Horizontal(AxisConstraint.Stretch(leftPad, rightPad));
        }

        /// <summary>
        /// Sizes the region's width as a fraction of the surface.
        /// </summary>
        /// <param name="startFraction">The left offset as a fraction of the surface width.</param>
        /// <param name="widthFraction">The width as a fraction of the surface width.</param>
        /// <returns>This builder.</returns>
        public RegionBuilder ProportionalWidth(double startFraction, double widthFraction)
        {
            return Horizontal(AxisConstraint.Proportional(startFraction, widthFraction));
        }

        /// <summary>
        /// Anchors the region a fixed distance from the top edge with a fixed height.
        /// </summary>
        /// <param name="top">The offset from the top edge.</param>
        /// <param name="height">The height in cells.</param>
        /// <returns>This builder.</returns>
        public RegionBuilder TopAnchored(int top, int height)
        {
            return Vertical(AxisConstraint.Fixed(top, height));
        }

        /// <summary>
        /// Anchors the region a fixed distance from the bottom edge with a fixed height.
        /// </summary>
        /// <param name="bottom">The offset from the bottom edge.</param>
        /// <param name="height">The height in cells.</param>
        /// <returns>This builder.</returns>
        public RegionBuilder BottomAnchored(int bottom, int height)
        {
            return Vertical(AxisConstraint.FromEnd(bottom, height));
        }

        /// <summary>
        /// Stretches the region across the height between the supplied pads.
        /// </summary>
        /// <param name="topPad">Cells reserved on the top. Defaults to 0.</param>
        /// <param name="bottomPad">Cells reserved on the bottom. Defaults to 0.</param>
        /// <returns>This builder.</returns>
        public RegionBuilder FillHeight(int topPad = 0, int bottomPad = 0)
        {
            return Vertical(AxisConstraint.Stretch(topPad, bottomPad));
        }

        /// <summary>
        /// Sizes the region's height as a fraction of the surface.
        /// </summary>
        /// <param name="startFraction">The top offset as a fraction of the surface height.</param>
        /// <param name="heightFraction">The height as a fraction of the surface height.</param>
        /// <returns>This builder.</returns>
        public RegionBuilder ProportionalHeight(double startFraction, double heightFraction)
        {
            return Vertical(AxisConstraint.Proportional(startFraction, heightFraction));
        }

        /// <summary>
        /// Sets the interior padding directly.
        /// </summary>
        /// <param name="padding">The padding.</param>
        /// <returns>This builder.</returns>
        public RegionBuilder WithPadding(Padding padding)
        {
            _Padding = padding;
            return this;
        }

        /// <summary>
        /// Sets a uniform interior padding on all four sides.
        /// </summary>
        /// <param name="all">The padding for every side. Must be zero or greater.</param>
        /// <returns>This builder.</returns>
        public RegionBuilder WithPadding(int all)
        {
            _Padding = Padding.Uniform(all);
            return this;
        }

        /// <summary>
        /// Sets the left, top, right, and bottom interior padding independently.
        /// </summary>
        /// <param name="left">The left padding. Must be zero or greater.</param>
        /// <param name="top">The top padding. Must be zero or greater.</param>
        /// <param name="right">The right padding. Must be zero or greater.</param>
        /// <param name="bottom">The bottom padding. Must be zero or greater.</param>
        /// <returns>This builder.</returns>
        public RegionBuilder WithPadding(int left, int top, int right, int bottom)
        {
            _Padding = new Padding(left, top, right, bottom);
            return this;
        }

        /// <summary>
        /// Sets the left and right interior padding, the empty space kept between the vertical borders
        /// and the region's contents.
        /// </summary>
        /// <param name="left">The left padding. Must be zero or greater.</param>
        /// <param name="right">The right padding. Must be zero or greater.</param>
        /// <returns>This builder.</returns>
        public RegionBuilder WithHorizontalPadding(int left, int right)
        {
            _Padding = new Padding(left, _Padding.Top, right, _Padding.Bottom);
            return this;
        }

        /// <summary>
        /// Builds the region.
        /// </summary>
        /// <returns>The constructed region.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when either the horizontal or vertical constraint has not been set.
        /// </exception>
        public Region Build()
        {
            if (_Horizontal == null)
                throw new InvalidOperationException("Region '" + _Id + "' has no horizontal constraint.");
            if (_Vertical == null)
                throw new InvalidOperationException("Region '" + _Id + "' has no vertical constraint.");

            return new Region(_Id, _Horizontal, _Vertical, _Padding);
        }
    }
}

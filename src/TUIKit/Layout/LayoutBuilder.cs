namespace TUIKit.Layout
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Fluent builder for a <see cref="Layout"/>. Add regions in draw order, then call
    /// <see cref="Build"/>. Beyond the raw <see cref="Add(Region)"/> overloads, the dock helpers
    /// (<see cref="DockTop"/>, <see cref="DockBottom"/>, <see cref="DockLeft"/>, <see cref="DockRight"/>,
    /// and <see cref="Fill"/>) build the common application shell — a top bar, a bottom bar, a sidebar,
    /// and a main content area — as real, non-overlapping regions. Each dock reserves an edge and the
    /// following docks and the fill honor the space already reserved, so you never compute rectangles by
    /// hand. Dock the edges first, then call <see cref="Fill"/> for the remaining center.
    /// </summary>
    public sealed class LayoutBuilder
    {
        private readonly List<Region> _Regions = new List<Region>();
        private int _Top;
        private int _Bottom;
        private int _Left;
        private int _Right;

        /// <summary>
        /// Adds a fully constructed region.
        /// </summary>
        /// <param name="region">The region. Must not be null.</param>
        /// <returns>This builder.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="region"/> is null.</exception>
        public LayoutBuilder Add(Region region)
        {
            if (region == null)
                throw new ArgumentNullException(nameof(region));

            _Regions.Add(region);
            return this;
        }

        /// <summary>
        /// Adds a region described by a builder callback.
        /// </summary>
        /// <param name="id">The region identifier. Must not be null or empty.</param>
        /// <param name="configure">A callback that configures the region. Must not be null.</param>
        /// <returns>This builder.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is null.</exception>
        public LayoutBuilder Add(string id, Action<RegionBuilder> configure)
        {
            if (configure == null)
                throw new ArgumentNullException(nameof(configure));

            RegionBuilder builder = new RegionBuilder(id);
            configure(builder);
            _Regions.Add(builder.Build());
            return this;
        }

        /// <summary>
        /// Docks a full-width bar of the supplied height against the top edge, below any bar already
        /// docked there. The region has no border or padding, so a single-row bar occupies exactly one
        /// cell.
        /// </summary>
        /// <param name="id">The region identifier. Must not be null or empty.</param>
        /// <param name="height">The bar height in cells. Must be greater than zero.</param>
        /// <returns>This builder.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="height"/> is not positive.</exception>
        public LayoutBuilder DockTop(string id, int height)
        {
            _Regions.Add(new Region(id, AxisConstraint.Stretch(_Left, _Right), AxisConstraint.Fixed(_Top, height)));
            _Top += height;
            return this;
        }

        /// <summary>
        /// Docks a full-width bar of the supplied height against the bottom edge, above any bar already
        /// docked there. The region has no border or padding.
        /// </summary>
        /// <param name="id">The region identifier. Must not be null or empty.</param>
        /// <param name="height">The bar height in cells. Must be greater than zero.</param>
        /// <returns>This builder.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="height"/> is not positive.</exception>
        public LayoutBuilder DockBottom(string id, int height)
        {
            _Regions.Add(new Region(id, AxisConstraint.Stretch(_Left, _Right), AxisConstraint.FromEnd(_Bottom, height)));
            _Bottom += height;
            return this;
        }

        /// <summary>
        /// Docks a sidebar of the supplied width against the left edge, to the right of any sidebar
        /// already docked there. The sidebar spans the height remaining between the top and bottom docks.
        /// The region has no border or padding.
        /// </summary>
        /// <param name="id">The region identifier. Must not be null or empty.</param>
        /// <param name="width">The sidebar width in cells. Must be greater than zero.</param>
        /// <returns>This builder.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="width"/> is not positive.</exception>
        public LayoutBuilder DockLeft(string id, int width)
        {
            _Regions.Add(new Region(id, AxisConstraint.Fixed(_Left, width), AxisConstraint.Stretch(_Top, _Bottom)));
            _Left += width;
            return this;
        }

        /// <summary>
        /// Docks a sidebar of the supplied width against the right edge, to the left of any sidebar
        /// already docked there. The sidebar spans the height remaining between the top and bottom docks.
        /// The region has no border or padding.
        /// </summary>
        /// <param name="id">The region identifier. Must not be null or empty.</param>
        /// <param name="width">The sidebar width in cells. Must be greater than zero.</param>
        /// <returns>This builder.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="width"/> is not positive.</exception>
        public LayoutBuilder DockRight(string id, int width)
        {
            _Regions.Add(new Region(id, AxisConstraint.FromEnd(_Right, width), AxisConstraint.Stretch(_Top, _Bottom)));
            _Right += width;
            return this;
        }

        /// <summary>
        /// Adds a region that fills the space remaining after every dock — the main content area. The
        /// region has no border or padding. Call this last, after docking the edges.
        /// </summary>
        /// <param name="id">The region identifier. Must not be null or empty.</param>
        /// <returns>This builder.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is null or empty.</exception>
        public LayoutBuilder Fill(string id)
        {
            _Regions.Add(new Region(id, AxisConstraint.Stretch(_Left, _Right), AxisConstraint.Stretch(_Top, _Bottom)));
            return this;
        }

        /// <summary>
        /// Builds the layout.
        /// </summary>
        /// <returns>The constructed layout.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no regions have been added.</exception>
        public Layout Build()
        {
            if (_Regions.Count == 0)
                throw new InvalidOperationException("A layout must contain at least one region.");

            return new Layout(_Regions);
        }
    }
}

namespace TUIKit.Layout
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Fluent builder for a <see cref="Layout"/>. Add regions in draw order, then call
    /// <see cref="Build"/>.
    /// </summary>
    public sealed class LayoutBuilder
    {
        private readonly List<Region> _Regions = new List<Region>();

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

namespace TUIKit.Layout
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// An ordered collection of <see cref="Region"/> definitions describing a whole screen layout.
    /// The layout resolves each region against the current surface size and derives the minimum
    /// surface required. Regions are drawn in order, so later regions render on top when they overlap.
    /// Instances are immutable; build them with <see cref="LayoutBuilder"/>.
    /// </summary>
    public sealed class Layout
    {
        private readonly Region[] _Regions;

        /// <summary>
        /// Gets the regions in draw order (earlier regions are painted first). Never null.
        /// </summary>
        public IReadOnlyList<Region> Regions
        {
            get { return _Regions; }
        }

        /// <summary>
        /// Gets the minimum surface size at which every region can be laid out. When the actual
        /// surface is smaller, the host shows the "terminal too small" screen instead of rendering.
        /// </summary>
        public Size MinimumSize { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Layout"/> class.
        /// </summary>
        /// <param name="regions">The regions in draw order. Must not be null, empty, or contain nulls.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="regions"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the collection is empty or contains a null.</exception>
        public Layout(IReadOnlyList<Region> regions)
        {
            if (regions == null)
                throw new ArgumentNullException(nameof(regions));
            if (regions.Count == 0)
                throw new ArgumentException("A layout must contain at least one region.", nameof(regions));

            Region[] copy = new Region[regions.Count];
            int minWidth = 0;
            int minHeight = 0;
            for (int i = 0; i < regions.Count; i++)
            {
                Region region = regions[i];
                if (region == null)
                    throw new ArgumentException("A layout region must not be null.", nameof(regions));

                copy[i] = region;
                Size regionMin = region.MinimumSize;
                if (regionMin.Width > minWidth)
                    minWidth = regionMin.Width;
                if (regionMin.Height > minHeight)
                    minHeight = regionMin.Height;
            }

            _Regions = copy;
            MinimumSize = new Size(minWidth, minHeight);
        }

        /// <summary>
        /// Begins fluent construction of a layout.
        /// </summary>
        /// <returns>A builder.</returns>
        public static LayoutBuilder Create()
        {
            return new LayoutBuilder();
        }

        /// <summary>
        /// Determines whether the supplied surface size is large enough to lay out every region.
        /// </summary>
        /// <param name="surface">The surface size.</param>
        /// <returns><c>true</c> when the surface meets <see cref="MinimumSize"/>; otherwise <c>false</c>.</returns>
        public bool FitsIn(Size surface)
        {
            return surface.Width >= MinimumSize.Width && surface.Height >= MinimumSize.Height;
        }

        /// <summary>
        /// Resolves a region's rectangle for the supplied surface, clipped to the surface bounds.
        /// </summary>
        /// <param name="region">The region to resolve. Must not be null and must belong to this layout.</param>
        /// <param name="surface">The surface size.</param>
        /// <returns>The clipped rectangle.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="region"/> is null.</exception>
        public Rect Resolve(Region region, Size surface)
        {
            if (region == null)
                throw new ArgumentNullException(nameof(region));

            Rect resolved = region.Resolve(surface);
            return resolved.Intersect(new Rect(0, 0, surface.Width, surface.Height));
        }

        /// <summary>
        /// Finds a region by identifier.
        /// </summary>
        /// <param name="id">The region identifier. Must not be null.</param>
        /// <returns>The region, or null when no region has that identifier.</returns>
        public Region? FindById(string id)
        {
            if (id == null)
                throw new ArgumentNullException(nameof(id));

            for (int i = 0; i < _Regions.Length; i++)
            {
                if (string.Equals(_Regions[i].Id, id, StringComparison.Ordinal))
                    return _Regions[i];
            }

            return null;
        }

        /// <summary>
        /// Determines whether any two regions overlap at the supplied surface size.
        /// </summary>
        /// <param name="surface">The surface size.</param>
        /// <returns><c>true</c> when at least one pair of regions overlaps; otherwise <c>false</c>.</returns>
        public bool HasOverlap(Size surface)
        {
            for (int i = 0; i < _Regions.Length; i++)
            {
                Rect a = Resolve(_Regions[i], surface);
                if (a.IsEmpty)
                    continue;

                for (int j = i + 1; j < _Regions.Length; j++)
                {
                    Rect b = Resolve(_Regions[j], surface);
                    if (!a.Intersect(b).IsEmpty)
                        return true;
                }
            }

            return false;
        }
    }
}

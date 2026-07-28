namespace TUIKit.Layout
{
    using System;

    /// <summary>
    /// A named rectangle in a layout, positioned and sized by an independent horizontal and vertical
    /// <see cref="AxisConstraint"/>. A layout is simply an arbitrary collection of regions; each one
    /// carries its own scaling behavior, so some regions can stay fixed while others grow with the
    /// surface. Instances are immutable; build them with <see cref="Define"/>.
    /// </summary>
    public sealed class Region
    {
        /// <summary>
        /// Gets the region identifier. Never null or empty.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets the horizontal constraint.
        /// </summary>
        public AxisConstraint Horizontal { get; }

        /// <summary>
        /// Gets the vertical constraint.
        /// </summary>
        public AxisConstraint Vertical { get; }

        /// <summary>
        /// Gets the padding reserved inside the region between its edges and its contents. Bound panes
        /// and widgets render into the region's content rectangle, which is the resolved rectangle
        /// inset by this padding.
        /// </summary>
        public Padding Padding { get; }

        /// <summary>
        /// Gets the minimum surface size at which this region can be laid out while respecting its
        /// minimum extents and padding.
        /// </summary>
        public Size MinimumSize
        {
            get { return new Size(Horizontal.MinimumExtent() + Padding.Horizontal, Vertical.MinimumExtent() + Padding.Vertical); }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Region"/> class.
        /// </summary>
        /// <param name="id">The region identifier. Must not be null or empty.</param>
        /// <param name="horizontal">The horizontal constraint. Must not be null.</param>
        /// <param name="vertical">The vertical constraint. Must not be null.</param>
        /// <param name="padding">The interior padding. Defaults to <see cref="Padding.Empty"/>.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is null or empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown when a constraint is null.</exception>
        public Region(string id, AxisConstraint horizontal, AxisConstraint vertical, Padding padding = default)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Region id must not be null or empty.", nameof(id));

            Id = id;
            Horizontal = horizontal ?? throw new ArgumentNullException(nameof(horizontal));
            Vertical = vertical ?? throw new ArgumentNullException(nameof(vertical));
            Padding = padding;
        }

        /// <summary>
        /// Begins fluent construction of a region.
        /// </summary>
        /// <param name="id">The region identifier. Must not be null or empty.</param>
        /// <returns>A builder.</returns>
        public static RegionBuilder Define(string id)
        {
            return new RegionBuilder(id);
        }

        /// <summary>
        /// Resolves this region's rectangle for the supplied surface size. The result is the intended
        /// rectangle and may extend past the surface when the surface is smaller than
        /// <see cref="MinimumSize"/>; callers clip as needed.
        /// </summary>
        /// <param name="surface">The surface size.</param>
        /// <returns>The resolved rectangle.</returns>
        public Rect Resolve(Size surface)
        {
            Horizontal.Resolve(surface.Width, out int x, out int width);
            Vertical.Resolve(surface.Height, out int y, out int height);
            return new Rect(x, y, width, height);
        }

        /// <summary>
        /// Resolves this region's content rectangle: the resolved rectangle inset by
        /// <see cref="Padding"/>. Bound panes and widgets render here so their contents stay clear of
        /// the region's edges.
        /// </summary>
        /// <param name="surface">The surface size.</param>
        /// <returns>The padded content rectangle.</returns>
        public Rect ContentRect(Size surface)
        {
            return Padding.Deflate(Resolve(surface));
        }
    }
}

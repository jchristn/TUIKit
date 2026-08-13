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
        /// Gets the padding reserved inside the region between its edges (or border) and its contents.
        /// Bound panes and widgets render into the region's content rectangle, which is the resolved
        /// rectangle inset by the border and then this padding.
        /// </summary>
        public Padding Padding { get; }

        /// <summary>
        /// Gets the border drawn around the region. Defaults to <see cref="BorderStyle.None"/>. When a
        /// border is present the content rectangle is inset by one cell on every side to make room for
        /// it, in addition to <see cref="Padding"/>.
        /// </summary>
        public BorderStyle Border { get; }

        /// <summary>
        /// Gets the title drawn on the region's top border, or null. Ignored when <see cref="Border"/>
        /// is <see cref="BorderStyle.None"/>.
        /// </summary>
        public string? BorderTitle { get; }

        /// <summary>
        /// Gets the explicit background color painted across the region's whole resolved rectangle
        /// (behind the border and any bound widget), or null when the region has no explicit
        /// background. An explicit background takes precedence over <see cref="BackgroundRole"/>. When
        /// both this and <see cref="BackgroundRole"/> are null the region is transparent and inherits
        /// the theme's text background, exactly as regions did before backgrounds existed.
        /// </summary>
        public Color? Background { get; }

        /// <summary>
        /// Gets the name of the theme style whose background is painted across the region when
        /// <see cref="Background"/> is null, or null for none. The host resolves the role against the
        /// active theme at render time (an unknown role falls back to the theme text background), so a
        /// theme switch restyles the region without any code change. Never an empty or whitespace
        /// string; the builder and constructor reject those.
        /// </summary>
        public string? BackgroundRole { get; }

        /// <summary>
        /// Gets a value indicating whether the region draws a border.
        /// </summary>
        public bool HasBorder
        {
            get { return Border != BorderStyle.None; }
        }

        /// <summary>
        /// Gets a value indicating whether the region carries any background (explicit color or theme
        /// role). When false the region is transparent and inherits the theme text background.
        /// </summary>
        public bool HasBackground
        {
            get { return Background.HasValue || BackgroundRole != null; }
        }

        /// <summary>
        /// Gets the minimum surface size at which this region can be laid out while respecting its
        /// minimum extents, border, and padding.
        /// </summary>
        public Size MinimumSize
        {
            get
            {
                int borderExtent = HasBorder ? 2 : 0;
                return new Size(
                    Horizontal.MinimumExtent() + Padding.Horizontal + borderExtent,
                    Vertical.MinimumExtent() + Padding.Vertical + borderExtent);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Region"/> class.
        /// </summary>
        /// <param name="id">The region identifier. Must not be null or empty.</param>
        /// <param name="horizontal">The horizontal constraint. Must not be null.</param>
        /// <param name="vertical">The vertical constraint. Must not be null.</param>
        /// <param name="padding">The interior padding. Defaults to <see cref="Padding.Empty"/>.</param>
        /// <param name="border">The border style. Defaults to <see cref="BorderStyle.None"/>.</param>
        /// <param name="borderTitle">The optional border title. Defaults to null.</param>
        /// <param name="background">
        /// The explicit background color, or null for none. Takes precedence over
        /// <paramref name="backgroundRole"/>. Defaults to null.
        /// </param>
        /// <param name="backgroundRole">
        /// The theme style name whose background paints the region when <paramref name="background"/>
        /// is null, or null for none. Defaults to null.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="id"/> is null or empty, or when <paramref name="backgroundRole"/>
        /// is a non-null empty or whitespace string.
        /// </exception>
        /// <exception cref="ArgumentNullException">Thrown when a constraint is null.</exception>
        public Region(string id, AxisConstraint horizontal, AxisConstraint vertical, Padding padding = default, BorderStyle border = BorderStyle.None, string? borderTitle = null, Color? background = null, string? backgroundRole = null)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Region id must not be null or empty.", nameof(id));
            if (backgroundRole != null && string.IsNullOrWhiteSpace(backgroundRole))
                throw new ArgumentException("Background role must not be empty or whitespace.", nameof(backgroundRole));

            Id = id;
            Horizontal = horizontal ?? throw new ArgumentNullException(nameof(horizontal));
            Vertical = vertical ?? throw new ArgumentNullException(nameof(vertical));
            Padding = padding;
            Border = border;
            BorderTitle = borderTitle;
            Background = background;
            BackgroundRole = backgroundRole;
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
            Rect rect = Resolve(surface);
            if (HasBorder && rect.Width >= 2 && rect.Height >= 2)
                rect = new Rect(rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2);

            return Padding.Deflate(rect);
        }
    }
}

namespace TUIKit
{
    /// <summary>
    /// A rectangular drawing target addressed in local, zero-based cell coordinates. Widgets render
    /// into an <see cref="ISurface"/> without knowing their absolute position on screen. Writes
    /// outside the surface bounds are silently clipped.
    /// </summary>
    /// <remarks>
    /// Implementations are not required to be thread-safe. The rendering engine renders on a single
    /// thread. Convenience helpers such as text drawing are provided as extension methods in
    /// <see cref="SurfaceExtensions"/> so the interface stays small and stable across framework
    /// targets that lack default interface methods.
    /// </remarks>
    public interface ISurface
    {
        /// <summary>
        /// Gets the drawable size of the surface in cells.
        /// </summary>
        Size Size { get; }

        /// <summary>
        /// Writes a single cell at the supplied local coordinate. Coordinates outside the surface
        /// bounds are ignored.
        /// </summary>
        /// <param name="x">The zero-based column.</param>
        /// <param name="y">The zero-based row.</param>
        /// <param name="cell">The cell to write.</param>
        void Set(int x, int y, Cell cell);

        /// <summary>
        /// Fills a rectangular region with the supplied cell. The region is clipped to the surface.
        /// </summary>
        /// <param name="region">The region to fill, in local coordinates.</param>
        /// <param name="cell">The cell to write into every position.</param>
        void Fill(Rect region, Cell cell);
    }
}

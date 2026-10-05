namespace TUIKit
{
    /// <summary>
    /// A surface whose cells can be read back as well as written. Drawing that merges with what is
    /// already there, such as joined box borders (<see cref="SurfaceExtensions.DrawJoinedBox(ISurface, Rect, CellStyle, BorderStyle, string?, CellStyle)"/>),
    /// needs it. <see cref="BufferSurface"/> and <see cref="SurfaceView"/> implement it; on a surface that
    /// does not, merging drawing falls back to plain drawing.
    /// </summary>
    public interface IReadableSurface : ISurface
    {
        /// <summary>
        /// Gets the cell at a position.
        /// </summary>
        /// <param name="x">The zero-based column.</param>
        /// <param name="y">The zero-based row.</param>
        /// <returns>The cell, or <see cref="Cell.Empty"/> outside the surface or when the cell cannot be read.</returns>
        Cell Get(int x, int y);
    }
}

namespace TUIKit
{
    using System;

    /// <summary>
    /// An <see cref="ISurface"/> backed by a <see cref="CellBuffer"/>, optionally offset and clipped
    /// to a sub-region of that buffer. A view lets a widget draw at local coordinates that map onto a
    /// slice of a larger buffer.
    /// </summary>
    /// <remarks>Not thread-safe; intended for use on the render thread.</remarks>
    public sealed class BufferSurface : ISurface
    {
        private readonly CellBuffer _Buffer;
        private readonly int _OffsetX;
        private readonly int _OffsetY;
        private readonly Size _Size;

        /// <inheritdoc/>
        public Size Size
        {
            get { return _Size; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BufferSurface"/> class covering an entire
        /// buffer.
        /// </summary>
        /// <param name="buffer">The backing buffer. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="buffer"/> is null.</exception>
        public BufferSurface(CellBuffer buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            _Buffer = buffer;
            _OffsetX = 0;
            _OffsetY = 0;
            _Size = buffer.Size;
        }

        private BufferSurface(CellBuffer buffer, int offsetX, int offsetY, Size size)
        {
            _Buffer = buffer;
            _OffsetX = offsetX;
            _OffsetY = offsetY;
            _Size = size;
        }

        /// <summary>
        /// Creates a view onto a sub-region of this surface. Writes to the view are translated into
        /// the parent surface and clipped to the region.
        /// </summary>
        /// <param name="region">The sub-region in this surface's local coordinates.</param>
        /// <returns>A surface whose origin is the region's top-left corner.</returns>
        public BufferSurface CreateView(Rect region)
        {
            Rect clipped = region.Intersect(new Rect(0, 0, _Size.Width, _Size.Height));
            return new BufferSurface(
                _Buffer,
                _OffsetX + clipped.X,
                _OffsetY + clipped.Y,
                new Size(clipped.Width, clipped.Height));
        }

        /// <inheritdoc/>
        public void Set(int x, int y, Cell cell)
        {
            if (x < 0 || x >= _Size.Width || y < 0 || y >= _Size.Height)
                return;

            _Buffer.Set(_OffsetX + x, _OffsetY + y, cell);
        }

        /// <inheritdoc/>
        public void Fill(Rect region, Cell cell)
        {
            Rect clipped = region.Intersect(new Rect(0, 0, _Size.Width, _Size.Height));
            for (int y = clipped.Top; y < clipped.Bottom; y++)
            {
                for (int x = clipped.Left; x < clipped.Right; x++)
                    Set(x, y, cell);
            }
        }
    }
}

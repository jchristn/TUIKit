namespace TUIKit
{
    using System;

    /// <summary>
    /// An <see cref="ISurface"/> that maps a rectangular sub-region of a parent surface into its own
    /// local, zero-based coordinate space. Container widgets use it to hand each child a clipped
    /// drawing area without exposing absolute coordinates. Writes outside the view are clipped, and
    /// the view never draws outside its bounds on the parent. Unlike <see cref="BufferSurface"/>'s
    /// view, this wraps any <see cref="ISurface"/>, so views can nest arbitrarily.
    /// </summary>
    public sealed class SurfaceView : ISurface
    {
        private readonly ISurface _Parent;
        private readonly int _OffsetX;
        private readonly int _OffsetY;
        private readonly int _Width;
        private readonly int _Height;

        /// <summary>
        /// Initializes a new instance of the <see cref="SurfaceView"/> class.
        /// </summary>
        /// <param name="parent">The parent surface. Must not be null.</param>
        /// <param name="bounds">The sub-region in the parent's coordinates.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="parent"/> is null.</exception>
        public SurfaceView(ISurface parent, Rect bounds)
        {
            _Parent = parent ?? throw new ArgumentNullException(nameof(parent));
            _OffsetX = bounds.X;
            _OffsetY = bounds.Y;
            _Width = Math.Max(0, bounds.Width);
            _Height = Math.Max(0, bounds.Height);
        }

        /// <inheritdoc/>
        public Size Size
        {
            get { return new Size(_Width, _Height); }
        }

        /// <inheritdoc/>
        public void Set(int x, int y, Cell cell)
        {
            if (x < 0 || y < 0 || x >= _Width || y >= _Height)
                return;

            _Parent.Set(_OffsetX + x, _OffsetY + y, cell);
        }

        /// <inheritdoc/>
        public void Fill(Rect region, Cell cell)
        {
            int x0 = Math.Max(0, region.X);
            int y0 = Math.Max(0, region.Y);
            int x1 = Math.Min(_Width, region.X + region.Width);
            int y1 = Math.Min(_Height, region.Y + region.Height);
            if (x1 <= x0 || y1 <= y0)
                return;

            _Parent.Fill(new Rect(_OffsetX + x0, _OffsetY + y0, x1 - x0, y1 - y0), cell);
        }
    }
}

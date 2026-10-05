namespace Test.Shared.Suites
{
    using System;
    using TUIKit;

    /// <summary>
    /// A surface that can be written but not read back, wrapping a <see cref="CellBuffer"/>. Used to check
    /// that drawing which merges with existing cells falls back to plain drawing on such surfaces.
    /// </summary>
    public sealed class WriteOnlySurface : ISurface
    {
        private readonly CellBuffer _Buffer;

        /// <summary>
        /// Initializes a new instance of the <see cref="WriteOnlySurface"/> class.
        /// </summary>
        /// <param name="buffer">The buffer written to. Must not be null.</param>
        public WriteOnlySurface(CellBuffer buffer)
        {
            _Buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        }

        /// <summary>
        /// Gets the buffer size.
        /// </summary>
        public Size Size
        {
            get { return _Buffer.Size; }
        }

        /// <summary>
        /// Writes a cell.
        /// </summary>
        /// <param name="x">The column.</param>
        /// <param name="y">The row.</param>
        /// <param name="cell">The cell.</param>
        public void Set(int x, int y, Cell cell)
        {
            _Buffer.Set(x, y, cell);
        }

        /// <summary>
        /// Fills a rectangle.
        /// </summary>
        /// <param name="region">The rectangle.</param>
        /// <param name="cell">The cell.</param>
        public void Fill(Rect region, Cell cell)
        {
            _Buffer.Fill(region, cell);
        }
    }
}

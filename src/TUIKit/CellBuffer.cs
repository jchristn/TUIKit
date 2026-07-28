namespace TUIKit
{
    using System;

    /// <summary>
    /// A mutable, row-major grid of <see cref="Cell"/> values representing a rectangular terminal
    /// surface. The buffer tracks which rows have changed since the last <see cref="ClearDirty"/>
    /// so a renderer can repaint only what moved.
    /// </summary>
    /// <remarks>
    /// Instances are not thread-safe. Callers that share a buffer across threads must provide their
    /// own synchronization; the rendering engine confines each buffer to its render thread.
    /// </remarks>
    public sealed class CellBuffer
    {
        private Cell[] _Cells;
        private bool[] _DirtyRows;
        private int _Width;
        private int _Height;

        /// <summary>
        /// Gets the width of the buffer in cells. Always zero or greater.
        /// </summary>
        public int Width
        {
            get { return _Width; }
        }

        /// <summary>
        /// Gets the height of the buffer in cells. Always zero or greater.
        /// </summary>
        public int Height
        {
            get { return _Height; }
        }

        /// <summary>
        /// Gets the size of the buffer.
        /// </summary>
        public Size Size
        {
            get { return new Size(_Width, _Height); }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CellBuffer"/> class filled with blank cells.
        /// </summary>
        /// <param name="width">The width in cells. Must be zero or greater.</param>
        /// <param name="height">The height in cells. Must be zero or greater.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="width"/> or <paramref name="height"/> is negative.
        /// </exception>
        public CellBuffer(int width, int height)
        {
            if (width < 0)
                throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be zero or greater.");
            if (height < 0)
                throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be zero or greater.");

            _Width = width;
            _Height = height;
            _Cells = new Cell[width * height];
            _DirtyRows = new bool[height];
            FillInternal(Cell.Empty);
            MarkAllDirty();
        }

        /// <summary>
        /// Gets the cell at the supplied coordinate.
        /// </summary>
        /// <param name="x">The zero-based column. Must be within the buffer bounds.</param>
        /// <param name="y">The zero-based row. Must be within the buffer bounds.</param>
        /// <returns>The cell at that coordinate.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the coordinate is out of bounds.</exception>
        public Cell Get(int x, int y)
        {
            if (x < 0 || x >= _Width)
                throw new ArgumentOutOfRangeException(nameof(x), x, "Column is outside the buffer bounds.");
            if (y < 0 || y >= _Height)
                throw new ArgumentOutOfRangeException(nameof(y), y, "Row is outside the buffer bounds.");

            return _Cells[(y * _Width) + x];
        }

        /// <summary>
        /// Sets the cell at the supplied coordinate and marks its row dirty when the value changes.
        /// Coordinates outside the buffer bounds are ignored so callers may clip freely.
        /// </summary>
        /// <param name="x">The zero-based column.</param>
        /// <param name="y">The zero-based row.</param>
        /// <param name="cell">The cell to write.</param>
        public void Set(int x, int y, Cell cell)
        {
            if (x < 0 || x >= _Width || y < 0 || y >= _Height)
                return;

            int index = (y * _Width) + x;
            if (!_Cells[index].Equals(cell))
            {
                _Cells[index] = cell;
                _DirtyRows[y] = true;
            }
        }

        /// <summary>
        /// Fills a rectangular region with the supplied cell. The region is clipped to the buffer.
        /// </summary>
        /// <param name="region">The region to fill.</param>
        /// <param name="cell">The cell to write into every position.</param>
        public void Fill(Rect region, Cell cell)
        {
            Rect clipped = region.Intersect(new Rect(0, 0, _Width, _Height));
            for (int y = clipped.Top; y < clipped.Bottom; y++)
            {
                for (int x = clipped.Left; x < clipped.Right; x++)
                    Set(x, y, cell);
            }
        }

        /// <summary>
        /// Clears the entire buffer to blank cells of the supplied style.
        /// </summary>
        /// <param name="style">The style to apply to every blank cell.</param>
        public void Clear(CellStyle style)
        {
            FillInternal(Cell.Blank(style));
            MarkAllDirty();
        }

        /// <summary>
        /// Resizes the buffer, preserving overlapping content and filling new area with blank cells.
        /// All rows are marked dirty.
        /// </summary>
        /// <param name="width">The new width in cells. Must be zero or greater.</param>
        /// <param name="height">The new height in cells. Must be zero or greater.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="width"/> or <paramref name="height"/> is negative.
        /// </exception>
        public void Resize(int width, int height)
        {
            if (width < 0)
                throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be zero or greater.");
            if (height < 0)
                throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be zero or greater.");

            if (width == _Width && height == _Height)
                return;

            Cell[] next = new Cell[width * height];
            for (int i = 0; i < next.Length; i++)
                next[i] = Cell.Empty;

            int copyWidth = Math.Min(width, _Width);
            int copyHeight = Math.Min(height, _Height);
            for (int y = 0; y < copyHeight; y++)
            {
                for (int x = 0; x < copyWidth; x++)
                    next[(y * width) + x] = _Cells[(y * _Width) + x];
            }

            _Cells = next;
            _DirtyRows = new bool[height];
            _Width = width;
            _Height = height;
            MarkAllDirty();
        }

        /// <summary>
        /// Determines whether the supplied row has changed since the last <see cref="ClearDirty"/>.
        /// </summary>
        /// <param name="row">The zero-based row.</param>
        /// <returns><c>true</c> when the row is dirty or out of bounds is treated as clean.</returns>
        public bool IsRowDirty(int row)
        {
            if (row < 0 || row >= _Height)
                return false;

            return _DirtyRows[row];
        }

        /// <summary>
        /// Marks every row as dirty, forcing a full repaint on the next diff.
        /// </summary>
        public void MarkAllDirty()
        {
            for (int y = 0; y < _Height; y++)
                _DirtyRows[y] = true;
        }

        /// <summary>
        /// Clears all dirty flags, marking the buffer as fully painted.
        /// </summary>
        public void ClearDirty()
        {
            for (int y = 0; y < _Height; y++)
                _DirtyRows[y] = false;
        }

        /// <summary>
        /// Copies the full contents of another buffer into this one. Both buffers must have the
        /// same dimensions. All rows are marked dirty.
        /// </summary>
        /// <param name="source">The buffer to copy from. Must not be null and must match dimensions.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the dimensions differ.</exception>
        public void CopyFrom(CellBuffer source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (source._Width != _Width || source._Height != _Height)
                throw new ArgumentException("Source buffer dimensions must match the destination.", nameof(source));

            Array.Copy(source._Cells, _Cells, _Cells.Length);
            MarkAllDirty();
        }

        private void FillInternal(Cell cell)
        {
            for (int i = 0; i < _Cells.Length; i++)
                _Cells[i] = cell;
        }
    }
}

namespace TUIKit
{
    using System;

    /// <summary>
    /// An immutable rectangle in terminal cell coordinates, defined by a top-left origin and a
    /// non-negative width and height. Instances are value types and are safe to share across threads.
    /// </summary>
    public readonly struct Rect : IEquatable<Rect>
    {
        /// <summary>
        /// Gets the zero-based column of the left edge.
        /// </summary>
        public int X { get; }

        /// <summary>
        /// Gets the zero-based row of the top edge.
        /// </summary>
        public int Y { get; }

        /// <summary>
        /// Gets the width in cells. Always zero or greater.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Gets the height in cells. Always zero or greater.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Gets the column of the left edge (equal to <see cref="X"/>).
        /// </summary>
        public int Left
        {
            get { return X; }
        }

        /// <summary>
        /// Gets the row of the top edge (equal to <see cref="Y"/>).
        /// </summary>
        public int Top
        {
            get { return Y; }
        }

        /// <summary>
        /// Gets the exclusive column just past the right edge (<see cref="X"/> + <see cref="Width"/>).
        /// </summary>
        public int Right
        {
            get { return X + Width; }
        }

        /// <summary>
        /// Gets the exclusive row just past the bottom edge (<see cref="Y"/> + <see cref="Height"/>).
        /// </summary>
        public int Bottom
        {
            get { return Y + Height; }
        }

        /// <summary>
        /// Gets a value indicating whether the rectangle encloses no cells.
        /// </summary>
        public bool IsEmpty
        {
            get { return Width == 0 || Height == 0; }
        }

        /// <summary>
        /// Gets the top-left corner of the rectangle.
        /// </summary>
        public Point Location
        {
            get { return new Point(X, Y); }
        }

        /// <summary>
        /// Gets the size of the rectangle.
        /// </summary>
        public Size Size
        {
            get { return new Size(Width, Height); }
        }

        /// <summary>
        /// Gets an empty rectangle at the origin.
        /// </summary>
        public static Rect Empty
        {
            get { return new Rect(0, 0, 0, 0); }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rect"/> struct.
        /// </summary>
        /// <param name="x">The column of the left edge.</param>
        /// <param name="y">The row of the top edge.</param>
        /// <param name="width">The width in cells. Must be zero or greater.</param>
        /// <param name="height">The height in cells. Must be zero or greater.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="width"/> or <paramref name="height"/> is negative.
        /// </exception>
        public Rect(int x, int y, int width, int height)
        {
            if (width < 0)
                throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be zero or greater.");
            if (height < 0)
                throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be zero or greater.");

            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rect"/> struct from a location and size.
        /// </summary>
        /// <param name="location">The top-left corner.</param>
        /// <param name="size">The size.</param>
        public Rect(Point location, Size size)
            : this(location.X, location.Y, size.Width, size.Height)
        {
        }

        /// <summary>
        /// Determines whether the rectangle contains the supplied point. The right and bottom
        /// edges are exclusive.
        /// </summary>
        /// <param name="point">The point to test.</param>
        /// <returns><c>true</c> if the point lies inside the rectangle; otherwise <c>false</c>.</returns>
        public bool Contains(Point point)
        {
            return point.X >= X && point.X < Right && point.Y >= Y && point.Y < Bottom;
        }

        /// <summary>
        /// Returns the intersection of this rectangle with another, or <see cref="Empty"/> when
        /// they do not overlap.
        /// </summary>
        /// <param name="other">The rectangle to intersect with.</param>
        /// <returns>The overlapping region, or an empty rectangle.</returns>
        public Rect Intersect(Rect other)
        {
            int left = Math.Max(X, other.X);
            int top = Math.Max(Y, other.Y);
            int right = Math.Min(Right, other.Right);
            int bottom = Math.Min(Bottom, other.Bottom);

            if (right <= left || bottom <= top)
                return Empty;

            return new Rect(left, top, right - left, bottom - top);
        }

        /// <summary>
        /// Returns a new rectangle translated by the supplied deltas.
        /// </summary>
        /// <param name="dx">The column delta.</param>
        /// <param name="dy">The row delta.</param>
        /// <returns>The translated rectangle.</returns>
        public Rect Offset(int dx, int dy)
        {
            return new Rect(X + dx, Y + dy, Width, Height);
        }

        /// <inheritdoc/>
        public bool Equals(Rect other)
        {
            return X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is Rect other && Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) ^ X;
                hash = (hash * 31) ^ Y;
                hash = (hash * 31) ^ Width;
                hash = (hash * 31) ^ Height;
                return hash;
            }
        }

        /// <summary>
        /// Determines whether two rectangles are equal.
        /// </summary>
        /// <param name="left">The first rectangle.</param>
        /// <param name="right">The second rectangle.</param>
        /// <returns><c>true</c> if the rectangles are equal; otherwise <c>false</c>.</returns>
        public static bool operator ==(Rect left, Rect right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Determines whether two rectangles are not equal.
        /// </summary>
        /// <param name="left">The first rectangle.</param>
        /// <param name="right">The second rectangle.</param>
        /// <returns><c>true</c> if the rectangles differ; otherwise <c>false</c>.</returns>
        public static bool operator !=(Rect left, Rect right)
        {
            return !left.Equals(right);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "(" + X + ", " + Y + ", " + Width + "x" + Height + ")";
        }
    }
}

namespace TUIKit
{
    using System;

    /// <summary>
    /// An immutable, zero-based cell coordinate on a terminal surface. Instances are value types
    /// and are safe to share across threads.
    /// </summary>
    public readonly struct Point : IEquatable<Point>
    {
        /// <summary>
        /// Gets the zero-based column (horizontal offset from the left edge).
        /// </summary>
        public int X { get; }

        /// <summary>
        /// Gets the zero-based row (vertical offset from the top edge).
        /// </summary>
        public int Y { get; }

        /// <summary>
        /// Gets the point at the origin (0, 0).
        /// </summary>
        public static Point Origin
        {
            get { return new Point(0, 0); }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Point"/> struct.
        /// </summary>
        /// <param name="x">The zero-based column.</param>
        /// <param name="y">The zero-based row.</param>
        public Point(int x, int y)
        {
            X = x;
            Y = y;
        }

        /// <summary>
        /// Returns a new point offset by the supplied deltas.
        /// </summary>
        /// <param name="dx">The column delta.</param>
        /// <param name="dy">The row delta.</param>
        /// <returns>The translated point.</returns>
        public Point Offset(int dx, int dy)
        {
            return new Point(X + dx, Y + dy);
        }

        /// <inheritdoc/>
        public bool Equals(Point other)
        {
            return X == other.X && Y == other.Y;
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is Point other && Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 397) ^ Y;
            }
        }

        /// <summary>
        /// Determines whether two points are equal.
        /// </summary>
        /// <param name="left">The first point.</param>
        /// <param name="right">The second point.</param>
        /// <returns><c>true</c> if the points are equal; otherwise <c>false</c>.</returns>
        public static bool operator ==(Point left, Point right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Determines whether two points are not equal.
        /// </summary>
        /// <param name="left">The first point.</param>
        /// <param name="right">The second point.</param>
        /// <returns><c>true</c> if the points differ; otherwise <c>false</c>.</returns>
        public static bool operator !=(Point left, Point right)
        {
            return !left.Equals(right);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "(" + X + ", " + Y + ")";
        }
    }
}

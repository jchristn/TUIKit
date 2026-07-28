namespace TUIKit
{
    using System;

    /// <summary>
    /// An immutable width and height measured in terminal cells. Both dimensions are non-negative.
    /// Instances are value types and are safe to share across threads.
    /// </summary>
    public readonly struct Size : IEquatable<Size>
    {
        /// <summary>
        /// Gets the width in cells. Always zero or greater.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Gets the height in cells. Always zero or greater.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Gets a value indicating whether the size has zero area (either dimension is zero).
        /// </summary>
        public bool IsEmpty
        {
            get { return Width == 0 || Height == 0; }
        }

        /// <summary>
        /// Gets a size of zero width and height.
        /// </summary>
        public static Size Empty
        {
            get { return new Size(0, 0); }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Size"/> struct.
        /// </summary>
        /// <param name="width">The width in cells. Must be zero or greater.</param>
        /// <param name="height">The height in cells. Must be zero or greater.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="width"/> or <paramref name="height"/> is negative.
        /// </exception>
        public Size(int width, int height)
        {
            if (width < 0)
                throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be zero or greater.");
            if (height < 0)
                throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be zero or greater.");

            Width = width;
            Height = height;
        }

        /// <inheritdoc/>
        public bool Equals(Size other)
        {
            return Width == other.Width && Height == other.Height;
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is Size other && Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                return (Width * 397) ^ Height;
            }
        }

        /// <summary>
        /// Determines whether two sizes are equal.
        /// </summary>
        /// <param name="left">The first size.</param>
        /// <param name="right">The second size.</param>
        /// <returns><c>true</c> if the sizes are equal; otherwise <c>false</c>.</returns>
        public static bool operator ==(Size left, Size right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Determines whether two sizes are not equal.
        /// </summary>
        /// <param name="left">The first size.</param>
        /// <param name="right">The second size.</param>
        /// <returns><c>true</c> if the sizes differ; otherwise <c>false</c>.</returns>
        public static bool operator !=(Size left, Size right)
        {
            return !left.Equals(right);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return Width + "x" + Height;
        }
    }
}

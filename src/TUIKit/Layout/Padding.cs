namespace TUIKit.Layout
{
    using System;
    using TUIKit;

    /// <summary>
    /// Empty space, measured in cells, reserved inside a rectangle between its edges and its contents.
    /// Left and right padding keep content off the vertical borders; top and bottom padding keep it off
    /// the horizontal borders. Instances are immutable value types.
    /// </summary>
    public readonly struct Padding : IEquatable<Padding>
    {
        /// <summary>
        /// Gets the number of empty cells reserved on the left. Always zero or greater.
        /// </summary>
        public int Left { get; }

        /// <summary>
        /// Gets the number of empty cells reserved on the top. Always zero or greater.
        /// </summary>
        public int Top { get; }

        /// <summary>
        /// Gets the number of empty cells reserved on the right. Always zero or greater.
        /// </summary>
        public int Right { get; }

        /// <summary>
        /// Gets the number of empty cells reserved on the bottom. Always zero or greater.
        /// </summary>
        public int Bottom { get; }

        /// <summary>
        /// Gets the total horizontal padding (left plus right).
        /// </summary>
        public int Horizontal
        {
            get { return Left + Right; }
        }

        /// <summary>
        /// Gets the total vertical padding (top plus bottom).
        /// </summary>
        public int Vertical
        {
            get { return Top + Bottom; }
        }

        /// <summary>
        /// Gets a padding of zero on every side.
        /// </summary>
        public static Padding Empty
        {
            get { return new Padding(0, 0, 0, 0); }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Padding"/> struct.
        /// </summary>
        /// <param name="left">The left padding. Must be zero or greater.</param>
        /// <param name="top">The top padding. Must be zero or greater.</param>
        /// <param name="right">The right padding. Must be zero or greater.</param>
        /// <param name="bottom">The bottom padding. Must be zero or greater.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when any value is negative.</exception>
        public Padding(int left, int top, int right, int bottom)
        {
            if (left < 0)
                throw new ArgumentOutOfRangeException(nameof(left), left, "Left padding must be zero or greater.");
            if (top < 0)
                throw new ArgumentOutOfRangeException(nameof(top), top, "Top padding must be zero or greater.");
            if (right < 0)
                throw new ArgumentOutOfRangeException(nameof(right), right, "Right padding must be zero or greater.");
            if (bottom < 0)
                throw new ArgumentOutOfRangeException(nameof(bottom), bottom, "Bottom padding must be zero or greater.");

            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        /// <summary>
        /// Creates a uniform padding on every side.
        /// </summary>
        /// <param name="all">The padding for every side. Must be zero or greater.</param>
        /// <returns>The padding.</returns>
        public static Padding Uniform(int all)
        {
            return new Padding(all, all, all, all);
        }

        /// <summary>
        /// Creates a symmetric padding with equal horizontal and equal vertical values.
        /// </summary>
        /// <param name="horizontal">The left and right padding. Must be zero or greater.</param>
        /// <param name="vertical">The top and bottom padding. Must be zero or greater.</param>
        /// <returns>The padding.</returns>
        public static Padding Symmetric(int horizontal, int vertical)
        {
            return new Padding(horizontal, vertical, horizontal, vertical);
        }

        /// <summary>
        /// Returns the supplied rectangle inset by this padding. The result never has negative width
        /// or height.
        /// </summary>
        /// <param name="rect">The rectangle to inset.</param>
        /// <returns>The inset rectangle.</returns>
        public Rect Deflate(Rect rect)
        {
            int width = Math.Max(0, rect.Width - Horizontal);
            int height = Math.Max(0, rect.Height - Vertical);
            return new Rect(rect.X + Left, rect.Y + Top, width, height);
        }

        /// <inheritdoc/>
        public bool Equals(Padding other)
        {
            return Left == other.Left && Top == other.Top && Right == other.Right && Bottom == other.Bottom;
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is Padding other && Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) ^ Left;
                hash = (hash * 31) ^ Top;
                hash = (hash * 31) ^ Right;
                hash = (hash * 31) ^ Bottom;
                return hash;
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "Padding(L" + Left + " T" + Top + " R" + Right + " B" + Bottom + ")";
        }
    }
}

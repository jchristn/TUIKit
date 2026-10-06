namespace TUIKit.Layout
{
    using System;

    /// <summary>
    /// Sizes one child of a stack (see <see cref="Widgets.FramedStack"/>) along the stack's axis, in content
    /// rows (vertical stacks) or columns (horizontal stacks). Border lines between children are added by
    /// the stack and are not part of the size. Create instances with <see cref="Fixed"/>,
    /// <see cref="Weighted"/>, or <see cref="Min"/>. Immutable and thread-safe.
    /// </summary>
    /// <remarks>
    /// Fixed children get their length first. The space left is shared by weighted children in proportion
    /// to their weights; a child whose share would fall below its minimum gets the minimum and the rest is
    /// shared again. Space left over after rounding goes to the last weighted child. When even the fixed
    /// lengths and minimums do not fit, the stack drops children from the end until they do.
    /// </remarks>
    public sealed class StackSize
    {
        private StackSize(StackSizeKind kind, int length, int weight, int minimum)
        {
            Kind = kind;
            Length = length;
            Weight = weight;
            Minimum = minimum;
        }

        /// <summary>
        /// Gets how the child is sized.
        /// </summary>
        public StackSizeKind Kind { get; }

        /// <summary>
        /// Gets the exact length of a <see cref="StackSizeKind.Fixed"/> size; zero for a weighted size.
        /// </summary>
        public int Length { get; }

        /// <summary>
        /// Gets the weight of a <see cref="StackSizeKind.Weight"/> size; zero for a fixed size.
        /// </summary>
        public int Weight { get; }

        /// <summary>
        /// Gets the smallest length the child accepts: <see cref="Length"/> for a fixed size, the minimum
        /// for a weighted size.
        /// </summary>
        public int Minimum { get; }

        /// <summary>
        /// Creates a size of exactly <paramref name="length"/> rows or columns.
        /// </summary>
        /// <param name="length">The length. Minimum 1, maximum 10000.</param>
        /// <returns>The size.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="length"/> is outside 1 to 10000.</exception>
        public static StackSize Fixed(int length)
        {
            if (length < 1 || length > 10000)
                throw new ArgumentOutOfRangeException(nameof(length), length, "Fixed length must be between 1 and 10000.");

            return new StackSize(StackSizeKind.Fixed, length, 0, length);
        }

        /// <summary>
        /// Creates a size that takes a share of the space left after fixed children, in proportion to
        /// <paramref name="weight"/>, and never less than <paramref name="min"/>.
        /// </summary>
        /// <param name="weight">The weight. Minimum 1, maximum 1000.</param>
        /// <param name="min">The minimum length. Defaults to 1. Minimum 1, maximum 10000.</param>
        /// <returns>The size.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="weight"/> is outside 1
        /// to 1000 or <paramref name="min"/> is outside 1 to 10000.</exception>
        public static StackSize Weighted(int weight, int min = 1)
        {
            if (weight < 1 || weight > 1000)
                throw new ArgumentOutOfRangeException(nameof(weight), weight, "Weight must be between 1 and 1000.");
            if (min < 1 || min > 10000)
                throw new ArgumentOutOfRangeException(nameof(min), min, "Minimum length must be between 1 and 10000.");

            return new StackSize(StackSizeKind.Weight, 0, weight, min);
        }

        /// <summary>
        /// Creates a size of at least <paramref name="length"/> that grows like <c>Weighted(1, length)</c>.
        /// </summary>
        /// <param name="length">The minimum length. Minimum 1, maximum 10000.</param>
        /// <returns>The size.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="length"/> is outside 1 to 10000.</exception>
        public static StackSize Min(int length)
        {
            if (length < 1 || length > 10000)
                throw new ArgumentOutOfRangeException(nameof(length), length, "Minimum length must be between 1 and 10000.");

            return new StackSize(StackSizeKind.Weight, 0, 1, length);
        }

        /// <summary>
        /// Returns a short description, such as <c>Fixed(5)</c> or <c>Weighted(2, min 1)</c>.
        /// </summary>
        /// <returns>The description.</returns>
        public override string ToString()
        {
            return Kind == StackSizeKind.Fixed ? "Fixed(" + Length + ")" : "Weighted(" + Weight + ", min " + Minimum + ")";
        }
    }
}

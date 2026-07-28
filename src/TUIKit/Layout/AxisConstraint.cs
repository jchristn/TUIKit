namespace TUIKit.Layout
{
    using System;

    /// <summary>
    /// Describes how a region is placed and sized along a single axis (horizontal or vertical) as a
    /// function of the surface size. Instances are immutable and are created through the static
    /// factory methods. This is the building block of the developer-defined region layout: any number
    /// of regions can each carry their own horizontal and vertical constraint.
    /// </summary>
    public sealed class AxisConstraint
    {
        private readonly int _P0;
        private readonly int _P1;
        private readonly double _F0;
        private readonly double _F1;
        private readonly int _Min;
        private readonly int _Max;

        /// <summary>
        /// Gets the mode that determines how the axis responds to surface size changes.
        /// </summary>
        public AxisMode Mode { get; }

        private AxisConstraint(AxisMode mode, int p0, int p1, double f0, double f1, int min, int max)
        {
            Mode = mode;
            _P0 = p0;
            _P1 = p1;
            _F0 = f0;
            _F1 = f1;
            _Min = min;
            _Max = max;
        }

        /// <summary>
        /// Creates a fixed constraint anchored to the start edge.
        /// </summary>
        /// <param name="offset">The offset from the start edge in cells. Must be zero or greater.</param>
        /// <param name="length">The length in cells. Must be greater than zero.</param>
        /// <returns>The constraint.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when an argument is out of range.</exception>
        public static AxisConstraint Fixed(int offset, int length)
        {
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset), offset, "Offset must be zero or greater.");
            if (length <= 0)
                throw new ArgumentOutOfRangeException(nameof(length), length, "Length must be greater than zero.");

            return new AxisConstraint(AxisMode.Fixed, offset, length, 0, 0, length, length);
        }

        /// <summary>
        /// Creates a fixed-length constraint anchored to the end edge.
        /// </summary>
        /// <param name="endOffset">The offset from the end edge in cells. Must be zero or greater.</param>
        /// <param name="length">The length in cells. Must be greater than zero.</param>
        /// <returns>The constraint.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when an argument is out of range.</exception>
        public static AxisConstraint FromEnd(int endOffset, int length)
        {
            if (endOffset < 0)
                throw new ArgumentOutOfRangeException(nameof(endOffset), endOffset, "End offset must be zero or greater.");
            if (length <= 0)
                throw new ArgumentOutOfRangeException(nameof(length), length, "Length must be greater than zero.");

            return new AxisConstraint(AxisMode.FromEnd, endOffset, length, 0, 0, length, length);
        }

        /// <summary>
        /// Creates a stretch constraint that fills the space between a start pad and an end pad.
        /// </summary>
        /// <param name="startPad">Cells reserved before the region. Must be zero or greater. Defaults to 0.</param>
        /// <param name="endPad">Cells reserved after the region. Must be zero or greater. Defaults to 0.</param>
        /// <param name="min">The minimum length in cells. Must be greater than zero. Defaults to 1.</param>
        /// <param name="max">The maximum length in cells, or <see cref="int.MaxValue"/> for unbounded. Defaults to unbounded.</param>
        /// <returns>The constraint.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when an argument is out of range.</exception>
        public static AxisConstraint Stretch(int startPad = 0, int endPad = 0, int min = 1, int max = int.MaxValue)
        {
            if (startPad < 0)
                throw new ArgumentOutOfRangeException(nameof(startPad), startPad, "Start pad must be zero or greater.");
            if (endPad < 0)
                throw new ArgumentOutOfRangeException(nameof(endPad), endPad, "End pad must be zero or greater.");
            if (min <= 0)
                throw new ArgumentOutOfRangeException(nameof(min), min, "Minimum length must be greater than zero.");
            if (max < min)
                throw new ArgumentOutOfRangeException(nameof(max), max, "Maximum length must be at least the minimum.");

            return new AxisConstraint(AxisMode.Stretch, startPad, endPad, 0, 0, min, max);
        }

        /// <summary>
        /// Creates a proportional constraint whose offset and length are fractions of the surface size.
        /// </summary>
        /// <param name="startFraction">The offset as a fraction of the surface (0.0 through 1.0).</param>
        /// <param name="lengthFraction">The length as a fraction of the surface (greater than 0.0 through 1.0).</param>
        /// <param name="min">The minimum length in cells. Must be greater than zero. Defaults to 1.</param>
        /// <param name="max">The maximum length in cells, or <see cref="int.MaxValue"/> for unbounded. Defaults to unbounded.</param>
        /// <returns>The constraint.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when an argument is out of range.</exception>
        public static AxisConstraint Proportional(double startFraction, double lengthFraction, int min = 1, int max = int.MaxValue)
        {
            if (startFraction < 0.0 || startFraction > 1.0)
                throw new ArgumentOutOfRangeException(nameof(startFraction), startFraction, "Start fraction must be within 0.0 and 1.0.");
            if (lengthFraction <= 0.0 || lengthFraction > 1.0)
                throw new ArgumentOutOfRangeException(nameof(lengthFraction), lengthFraction, "Length fraction must be within 0.0 (exclusive) and 1.0.");
            if (min <= 0)
                throw new ArgumentOutOfRangeException(nameof(min), min, "Minimum length must be greater than zero.");
            if (max < min)
                throw new ArgumentOutOfRangeException(nameof(max), max, "Maximum length must be at least the minimum.");

            return new AxisConstraint(AxisMode.Proportional, 0, 0, startFraction, lengthFraction, min, max);
        }

        /// <summary>
        /// Resolves the offset and length for this axis given the total surface extent.
        /// </summary>
        /// <param name="total">The total surface extent along this axis in cells.</param>
        /// <param name="offset">Receives the resolved start offset.</param>
        /// <param name="length">Receives the resolved length.</param>
        public void Resolve(int total, out int offset, out int length)
        {
            switch (Mode)
            {
                case AxisMode.Fixed:
                    offset = _P0;
                    length = Clamp(_P1);
                    break;
                case AxisMode.FromEnd:
                    length = Clamp(_P1);
                    offset = total - _P0 - length;
                    break;
                case AxisMode.Stretch:
                    offset = _P0;
                    length = Clamp(total - _P0 - _P1);
                    break;
                case AxisMode.Proportional:
                    offset = (int)Math.Round(_F0 * total, MidpointRounding.AwayFromZero);
                    length = Clamp((int)Math.Round(_F1 * total, MidpointRounding.AwayFromZero));
                    break;
                default:
                    offset = 0;
                    length = Clamp(total);
                    break;
            }

            if (offset < 0)
                offset = 0;
        }

        /// <summary>
        /// Gets the minimum surface extent along this axis at which the region can be laid out while
        /// respecting its minimum length.
        /// </summary>
        /// <returns>The minimum extent in cells.</returns>
        public int MinimumExtent()
        {
            switch (Mode)
            {
                case AxisMode.Fixed:
                    return _P0 + _P1;
                case AxisMode.FromEnd:
                    return _P0 + _P1;
                case AxisMode.Stretch:
                    return _P0 + _P1 + _Min;
                case AxisMode.Proportional:
                    return _F1 > 0.0 ? (int)Math.Ceiling(_Min / _F1) : _Min;
                default:
                    return _Min;
            }
        }

        private int Clamp(int length)
        {
            int value = length;
            if (value < _Min)
                value = _Min;
            if (value > _Max)
                value = _Max;
            if (value < 0)
                value = 0;

            return value;
        }
    }
}

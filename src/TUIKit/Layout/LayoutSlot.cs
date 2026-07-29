namespace TUIKit.Layout
{
    using System;

    /// <summary>
    /// One slot in a <see cref="Layout.Row"/> or <see cref="Layout.Column"/>. A slot is either a fixed
    /// number of cells or a fill slot that shares the remaining space equally with the other fill
    /// slots. Instances are immutable.
    /// </summary>
    public sealed class LayoutSlot
    {
        /// <summary>
        /// Gets the region identifier for this slot. Never null or empty.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets a value indicating whether the slot fills the remaining space.
        /// </summary>
        public bool IsFill { get; }

        /// <summary>
        /// Gets the fixed size in cells for a non-fill slot; zero for a fill slot.
        /// </summary>
        public int Cells { get; }

        private LayoutSlot(string id, bool isFill, int cells)
        {
            Id = id;
            IsFill = isFill;
            Cells = cells;
        }

        /// <summary>
        /// Creates a fixed-size slot.
        /// </summary>
        /// <param name="id">The region identifier. Must not be null or empty.</param>
        /// <param name="cells">The size in cells. Must be greater than zero.</param>
        /// <returns>The slot.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="cells"/> is not positive.</exception>
        public static LayoutSlot Fixed(string id, int cells)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Slot id must not be null or empty.", nameof(id));
            if (cells <= 0)
                throw new ArgumentOutOfRangeException(nameof(cells), cells, "Cells must be greater than zero.");

            return new LayoutSlot(id, false, cells);
        }

        /// <summary>
        /// Creates a fill slot that shares the remaining space equally with other fill slots.
        /// </summary>
        /// <param name="id">The region identifier. Must not be null or empty.</param>
        /// <returns>The slot.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is null or empty.</exception>
        public static LayoutSlot Fill(string id)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Slot id must not be null or empty.", nameof(id));

            return new LayoutSlot(id, true, 0);
        }
    }
}

namespace TUIKit.Widgets
{
    using System;

    /// <summary>
    /// A single row in a <see cref="DefinitionList"/>: either a labeled value or a section header.
    /// Instances are immutable.
    /// </summary>
    public sealed class DefinitionRow
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DefinitionRow"/> class.
        /// </summary>
        /// <param name="label">The row label, or the section title when <paramref name="isSection"/> is true. Must not be null.</param>
        /// <param name="value">The row value, or null for a section header.</param>
        /// <param name="isSection">Whether the row is a section header rather than a labeled value.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="label"/> is null.</exception>
        public DefinitionRow(string label, string? value, bool isSection)
        {
            Label = label ?? throw new ArgumentNullException(nameof(label));
            Value = value;
            IsSection = isSection;
        }

        /// <summary>
        /// Gets the row label, or the section title for a section header. Never null.
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// Gets the row value, or null for a section header.
        /// </summary>
        public string? Value { get; }

        /// <summary>
        /// Gets a value indicating whether the row is a section header.
        /// </summary>
        public bool IsSection { get; }
    }
}

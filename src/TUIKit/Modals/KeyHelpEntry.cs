namespace TUIKit.Modals
{
    using System;

    /// <summary>
    /// One row of a <see cref="KeyHelpModal"/>: a category (group heading), the keys, and what they do.
    /// Instances are immutable.
    /// </summary>
    public sealed class KeyHelpEntry
    {
        /// <summary>
        /// Gets the category. Never null.
        /// </summary>
        public string Category { get; }

        /// <summary>
        /// Gets the key label, for example <c>Ctrl+P</c> or <c>Up/Down</c>. Never null.
        /// </summary>
        public string Keys { get; }

        /// <summary>
        /// Gets the description. Never null.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="KeyHelpEntry"/> class.
        /// </summary>
        /// <param name="category">The category. Must not be null.</param>
        /// <param name="keys">The key label. Must not be null.</param>
        /// <param name="description">The description. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public KeyHelpEntry(string category, string keys, string description)
        {
            Category = category ?? throw new ArgumentNullException(nameof(category));
            Keys = keys ?? throw new ArgumentNullException(nameof(keys));
            Description = description ?? throw new ArgumentNullException(nameof(description));
        }
    }
}

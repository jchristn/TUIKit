namespace TUIKit.Widgets
{
    using System;

    /// <summary>
    /// A single entry in a <see cref="Menu"/>: a label, an optional action to run when it is
    /// activated, and an enabled flag. Disabled items render dimmed and cannot be activated.
    /// </summary>
    public sealed class MenuItem
    {
        /// <summary>Gets the item label.</summary>
        public string Label { get; }

        /// <summary>Gets the action invoked when the item is activated, or null.</summary>
        public Action? Action { get; }

        /// <summary>Gets or sets whether the item can be activated. Defaults to true.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="MenuItem"/> class.
        /// </summary>
        /// <param name="label">The label. Must not be null.</param>
        /// <param name="action">The action to run on activation, or null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="label"/> is null.</exception>
        public MenuItem(string label, Action? action = null)
        {
            Label = label ?? throw new ArgumentNullException(nameof(label));
            Action = action;
        }
    }
}

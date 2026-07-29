namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A named drop-down menu: a title and an ordered list of <see cref="MenuItem"/> entries. Build
    /// one with the fluent <see cref="Add(string, Action)"/> helper, then add it to a
    /// <see cref="MenuBar"/>.
    /// </summary>
    public sealed class Menu
    {
        private readonly List<MenuItem> _Items = new List<MenuItem>();

        /// <summary>Gets the menu title shown in the bar.</summary>
        public string Title { get; }

        /// <summary>Gets the menu items.</summary>
        public IReadOnlyList<MenuItem> Items
        {
            get { return _Items; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Menu"/> class.
        /// </summary>
        /// <param name="title">The menu title. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="title"/> is null.</exception>
        public Menu(string title)
        {
            Title = title ?? throw new ArgumentNullException(nameof(title));
        }

        /// <summary>
        /// Adds an item.
        /// </summary>
        /// <param name="label">The item label. Must not be null.</param>
        /// <param name="action">The action to run on activation, or null.</param>
        /// <returns>This menu, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="label"/> is null.</exception>
        public Menu Add(string label, Action? action = null)
        {
            _Items.Add(new MenuItem(label, action));
            return this;
        }

        /// <summary>
        /// Adds a pre-built item.
        /// </summary>
        /// <param name="item">The item. Must not be null.</param>
        /// <returns>This menu, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is null.</exception>
        public Menu Add(MenuItem item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            _Items.Add(item);
            return this;
        }
    }
}

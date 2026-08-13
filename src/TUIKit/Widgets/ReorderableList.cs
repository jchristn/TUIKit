namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;

    /// <summary>
    /// A selectable list whose items can be reordered and removed from the keyboard: move the selected
    /// item up or down (Alt+Up / Alt+Down, or "[" / "]") and delete it (Delete or "d"). Use it for
    /// queues, playlists, priority lists, and column ordering. The current order is read back through
    /// <see cref="Order"/>, and <see cref="Reordered"/> / <see cref="Removed"/> report changes.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    public sealed class ReorderableList<T> : IWidget, IFocusable, IFocusAware
    {
        private readonly ListView<T> _List;
        private readonly List<T> _Items = new List<T>();

        /// <summary>
        /// Initializes a new instance of the <see cref="ReorderableList{T}"/> class.
        /// </summary>
        /// <param name="items">The initial items. Must not be null.</param>
        /// <param name="display">
        /// A selector mapping an item to its label, or null when <typeparamref name="T"/> is <see cref="string"/>.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="items"/> is null, or when <paramref name="display"/> is null and
        /// <typeparamref name="T"/> is not <see cref="string"/>.
        /// </exception>
        public ReorderableList(IEnumerable<T> items, Func<T, string>? display = null)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            _List = new ListView<T>(display);
            _Items.AddRange(items);
            _List.SetItems(_Items);
        }

        /// <summary>
        /// Raised after the selected item is moved, with its new index.
        /// </summary>
        public event Action<int>? Reordered;

        /// <summary>
        /// Raised after an item is removed, with the removed item.
        /// </summary>
        public event Action<T>? Removed;

        /// <summary>
        /// Gets or sets a value indicating whether the list is focused. Forwarded to the inner list.
        /// </summary>
        public bool IsFocused
        {
            get { return _List.IsFocused; }
            set { _List.IsFocused = value; }
        }

        /// <summary>
        /// Gets the current order of items. Never null.
        /// </summary>
        public IReadOnlyList<T> Order
        {
            get { return new List<T>(_Items); }
        }

        /// <summary>
        /// Gets the zero-based selected index, or -1 when empty.
        /// </summary>
        public int SelectedIndex
        {
            get { return _List.SelectedIndex; }
        }

        /// <summary>
        /// Moves the selected item up one position, if possible.
        /// </summary>
        /// <returns><c>true</c> when the item moved; otherwise <c>false</c>.</returns>
        public bool MoveUp()
        {
            int sel = _List.SelectedIndex;
            if (sel <= 0)
                return false;

            Swap(sel, sel - 1);
            SelectIndex(sel - 1);
            Reordered?.Invoke(sel - 1);
            return true;
        }

        /// <summary>
        /// Moves the selected item down one position, if possible.
        /// </summary>
        /// <returns><c>true</c> when the item moved; otherwise <c>false</c>.</returns>
        public bool MoveDown()
        {
            int sel = _List.SelectedIndex;
            if (sel < 0 || sel >= _Items.Count - 1)
                return false;

            Swap(sel, sel + 1);
            SelectIndex(sel + 1);
            Reordered?.Invoke(sel + 1);
            return true;
        }

        /// <summary>
        /// Removes the selected item, if any.
        /// </summary>
        /// <returns><c>true</c> when an item was removed; otherwise <c>false</c>.</returns>
        public bool RemoveSelected()
        {
            int sel = _List.SelectedIndex;
            if (sel < 0)
                return false;

            T removed = _Items[sel];
            _Items.RemoveAt(sel);
            SelectIndex(Math.Min(sel, _Items.Count - 1));
            Removed?.Invoke(removed);
            return true;
        }

        /// <inheritdoc/>
        public void OnFocusChanged(bool focused)
        {
            _List.OnFocusChanged(focused);
        }

        /// <inheritdoc/>
        public bool HandleKey(KeyEvent key)
        {
            bool alt = (key.Modifiers & KeyModifiers.Alt) == KeyModifiers.Alt;

            if ((key.Code == KeyCode.Up && alt) || (key.Code == KeyCode.Character && key.Rune == '['))
                return MoveUp();

            if ((key.Code == KeyCode.Down && alt) || (key.Code == KeyCode.Character && key.Rune == ']'))
                return MoveDown();

            if (key.Code == KeyCode.Delete || (key.Code == KeyCode.Character && key.Rune == 'd'))
                return RemoveSelected();

            return _List.HandleKey(key);
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            return _List.Measure(available);
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            _List.Render(surface);
        }

        private void Swap(int a, int b)
        {
            T temp = _Items[a];
            _Items[a] = _Items[b];
            _Items[b] = temp;
        }

        private void SelectIndex(int index)
        {
            _List.SetItems(_Items);
            for (int i = 0; i < index; i++)
                _List.SelectNext();
        }
    }
}

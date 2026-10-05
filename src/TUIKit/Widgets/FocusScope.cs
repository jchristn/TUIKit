namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit.Input;

    /// <summary>
    /// A hierarchical focus ring: an ordered set of focusable children with one focused child, used by
    /// container widgets (and applications) to build nested focus scopes. Keys go to the focused child
    /// first; when the child does not consume Tab or Shift+Tab, the scope moves focus to the next or
    /// previous child, descending into children that implement <see cref="IFocusContainer"/>. At either
    /// end the scope reports that focus should leave it (unless <see cref="Wrap"/> is set), so a parent
    /// scope moves on. Disabled children (<see cref="IEnableable.IsEnabled"/> false) are skipped.
    /// Children implementing <see cref="IFocusAware"/> are told when they gain or lose focus, taking the
    /// scope's own <see cref="IsActive"/> state into account so only the leaf on the active path shows focus.
    /// </summary>
    /// <remarks>Not thread-safe: use it from the UI loop.</remarks>
    public sealed class FocusScope : IFocusContainer, IFocusAware
    {
        private readonly List<IFocusable> _Children = new List<IFocusable>();
        private int _Index = -1;
        private bool _Active = true;

        /// <summary>
        /// Raised after the focused child changes. The argument is the new focused child, or null.
        /// </summary>
        public event Action<IFocusable?>? FocusMoved;

        /// <summary>
        /// Gets or sets a value indicating whether Tab traversal wraps from the last child to the first
        /// (and back) instead of reporting that focus should leave the scope. Defaults to false, which is
        /// what nested scopes want; set it for a top-level ring.
        /// </summary>
        public bool Wrap { get; set; }

        /// <summary>
        /// Gets the children in focus order. Never null.
        /// </summary>
        public IReadOnlyList<IFocusable> Children
        {
            get { return _Children; }
        }

        /// <summary>
        /// Gets the number of children.
        /// </summary>
        public int Count
        {
            get { return _Children.Count; }
        }

        /// <summary>
        /// Gets the zero-based index of the focused child, or -1 when there is none.
        /// </summary>
        public int FocusedIndex
        {
            get { return _Index; }
        }

        /// <summary>
        /// Gets the focused child, or null.
        /// </summary>
        public IFocusable? Focused
        {
            get { return _Index >= 0 && _Index < _Children.Count ? _Children[_Index] : null; }
        }

        /// <inheritdoc/>
        public IFocusable? FocusedLeaf
        {
            get
            {
                IFocusable? focused = Focused;
                if (focused is IFocusContainer container)
                    return container.FocusedLeaf ?? focused;

                return focused;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the scope itself is on the active focus path. The owner of a
        /// nested scope forwards its own focus changes through <see cref="OnFocusChanged"/>. Defaults to true.
        /// </summary>
        public bool IsActive
        {
            get { return _Active; }
        }

        /// <summary>
        /// Adds a child at the end of the focus order. The first enabled child added becomes focused.
        /// </summary>
        /// <typeparam name="TChild">The child type.</typeparam>
        /// <param name="child">The child. Must not be null.</param>
        /// <returns>The child, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="child"/> is null.</exception>
        public TChild Add<TChild>(TChild child) where TChild : IFocusable
        {
            if (child == null)
                throw new ArgumentNullException(nameof(child));

            _Children.Add(child);
            if (_Index < 0 && IsFocusable(child))
            {
                _Index = _Children.Count - 1;
                Notify(child, _Active);
            }
            else
            {
                Notify(child, false);
            }

            return child;
        }

        /// <summary>
        /// Removes a child. When it was focused, focus moves to the next focusable child.
        /// </summary>
        /// <param name="child">The child. Must not be null.</param>
        /// <returns><c>true</c> when the child was removed; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="child"/> is null.</exception>
        public bool Remove(IFocusable child)
        {
            if (child == null)
                throw new ArgumentNullException(nameof(child));

            int index = _Children.IndexOf(child);
            if (index < 0)
                return false;

            bool wasFocused = index == _Index;
            _Children.RemoveAt(index);
            if (wasFocused)
            {
                Notify(child, false);
                _Index = -1;
                int candidate = FindFrom(Math.Min(index, _Children.Count - 1), 1, true);
                if (candidate < 0)
                    candidate = FindFrom(_Children.Count - 1, -1, true);
                SetIndex(candidate);
            }
            else if (index < _Index)
            {
                _Index--;
            }

            return true;
        }

        /// <summary>
        /// Removes every child and clears focus.
        /// </summary>
        public void Clear()
        {
            IFocusable? focused = Focused;
            _Children.Clear();
            _Index = -1;
            if (focused != null)
                Notify(focused, false);
        }

        /// <summary>
        /// Focuses a child. Disabled children can still be focused explicitly.
        /// </summary>
        /// <param name="child">The child. Must not be null and must belong to the scope.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="child"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="child"/> is not in the scope.</exception>
        public void SetFocus(IFocusable child)
        {
            if (child == null)
                throw new ArgumentNullException(nameof(child));

            int index = _Children.IndexOf(child);
            if (index < 0)
                throw new ArgumentException("The widget is not a child of this focus scope.", nameof(child));

            SetIndex(index);
        }

        /// <summary>
        /// Focuses the child at an index.
        /// </summary>
        /// <param name="index">The zero-based index. Must be within [0, Count).</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is out of range.</exception>
        public void SetFocus(int index)
        {
            if (index < 0 || index >= _Children.Count)
                throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be within [0, Count).");

            SetIndex(index);
        }

        /// <inheritdoc/>
        public bool MoveFocus(bool forward)
        {
            if (Focused is IFocusContainer container && IsFocusable(container) && container.MoveFocus(forward))
                return true;

            int step = forward ? 1 : -1;
            int next = FindFrom(_Index + step, step, false);
            if (next < 0)
            {
                if (!Wrap)
                    return false;

                next = FindFrom(forward ? 0 : _Children.Count - 1, step, false);
                if (next < 0)
                    return false;
            }

            SetIndex(next);
            if (_Children[next] is IFocusContainer entered)
                entered.FocusEdge(forward);
            return true;
        }

        /// <inheritdoc/>
        public void FocusEdge(bool first)
        {
            int index = FindFrom(first ? 0 : _Children.Count - 1, first ? 1 : -1, true);
            if (index < 0)
                return;

            SetIndex(index);
            if (_Children[index] is IFocusContainer container)
                container.FocusEdge(first);
        }

        /// <summary>
        /// Routes a key: the focused child gets first refusal; an unconsumed Tab or Shift+Tab moves focus
        /// within the scope.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns>
        /// <c>true</c> when the key was consumed; <c>false</c> when it was not, including Tab at the edge of
        /// the scope (so the parent moves focus on).
        /// </returns>
        public bool HandleKey(KeyEvent key)
        {
            IFocusable? focused = Focused;
            if (focused != null && IsFocusable(focused) && focused.HandleKey(key))
                return true;

            if (key.Code == KeyCode.Tab && (key.Modifiers & ~KeyModifiers.Shift) == KeyModifiers.None)
                return MoveFocus((key.Modifiers & KeyModifiers.Shift) == 0);

            return false;
        }

        /// <summary>
        /// Marks the scope as on or off the active focus path and forwards the change to the focused
        /// child, so its rendered focus state follows. Owners call this from their own
        /// <see cref="IFocusAware.OnFocusChanged"/>.
        /// </summary>
        /// <param name="focused"><c>true</c> when the owner gained focus; otherwise <c>false</c>.</param>
        public void OnFocusChanged(bool focused)
        {
            if (_Active == focused)
                return;

            _Active = focused;
            IFocusable? current = Focused;
            if (current != null)
                Notify(current, focused);
        }

        /// <summary>
        /// Gets a value indicating whether a widget can take focus by traversal: it is not disabled.
        /// </summary>
        /// <param name="widget">The widget, or null.</param>
        /// <returns><c>true</c> when the widget is non-null and enabled.</returns>
        public static bool IsFocusable(object? widget)
        {
            if (widget == null)
                return false;

            return !(widget is IEnableable enableable) || enableable.IsEnabled;
        }

        private int FindFrom(int start, int step, bool allowCurrent)
        {
            for (int i = start; i >= 0 && i < _Children.Count; i += step)
            {
                if (!allowCurrent && i == _Index)
                    continue;

                if (IsFocusable(_Children[i]))
                    return i;
            }

            return -1;
        }

        private void SetIndex(int index)
        {
            if (index == _Index)
                return;

            IFocusable? previous = Focused;
            _Index = index;
            if (previous != null)
                Notify(previous, false);

            IFocusable? current = Focused;
            if (current != null)
                Notify(current, _Active);

            FocusMoved?.Invoke(current);
        }

        private static void Notify(IFocusable widget, bool focused)
        {
            if (widget is IFocusAware aware)
                aware.OnFocusChanged(focused);
        }
    }
}

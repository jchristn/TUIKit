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
    /// Children that implement <see cref="IFocusStop"/> with <see cref="IFocusStop.IsFocusStop"/> false are
    /// skipped by Tab but can still be focused with <see cref="SetFocus(IFocusable)"/>. When the focused
    /// child becomes hidden, disabled, or empty, <see cref="RepairFocus"/> moves focus to a sibling; the
    /// host calls it after layout for every scope on the focus path (see <see cref="AutoRepair"/>).
    /// </summary>
    /// <remarks>Not thread-safe: use it from the UI loop.</remarks>
    public sealed class FocusScope : IFocusContainer, IFocusAware, IFocusPathNode, IFocusChildren
    {
        private readonly List<IFocusable> _Children = new List<IFocusable>();
        private int _Index = -1;
        private bool _Active = true;
        private bool _Silent;

        /// <summary>
        /// Initializes a new instance of the <see cref="FocusScope"/> class that is active from
        /// construction, as in 1.4.0: the first focusable child added is told it has focus at once.
        /// </summary>
        public FocusScope()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FocusScope"/> class that may start inactive. An
        /// inactive scope still tracks a focused child, but sends no <see cref="IFocusAware.OnFocusChanged"/>
        /// calls (not even on <see cref="Add{TChild}"/>) until it is entered with
        /// <see cref="OnFocusChanged"/>(true). Use it for a scope built off screen, so its children do not
        /// show focus before the user reaches them.
        /// </summary>
        /// <param name="startsActive"><c>true</c> for the 1.4.0 behavior; <c>false</c> to start inactive.</param>
        public FocusScope(bool startsActive)
        {
            _Active = startsActive;
            _Silent = !startsActive;
        }

        /// <summary>
        /// Raised after the focused child changes. The argument is the new focused child, or null.
        /// </summary>
        public event Action<IFocusable?>? FocusMoved;

        /// <summary>
        /// Gets or sets a value indicating whether the host repairs this scope's focus after each layout
        /// when the scope is on the focus path (see <see cref="RepairFocus"/>). Defaults to true. Set false
        /// to keep the 1.4.0 behavior, where focus stays on a child that became hidden or disabled.
        /// <see cref="Remove"/> always moves focus off a removed child, as before.
        /// </summary>
        public bool AutoRepair { get; set; } = true;

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
        /// Gets the child that holds focus one level down, or null when the scope has no focused child. Part of
        /// <see cref="IFocusPathNode"/>; the host uses it to build <see cref="FocusPath"/>.
        /// </summary>
        public IFocusable? FocusedChild
        {
            get { return Focused; }
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
            if (_Index < 0 && IsAddCandidate(child))
            {
                _Index = _Children.Count - 1;
                if (!_Silent)
                    Notify(child, _Active);
            }
            else if (!_Silent)
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
            if (focused)
                _Silent = false;
            if (_Active == focused)
                return;

            _Active = focused;
            IFocusable? current = Focused;
            if (current != null)
                Notify(current, focused);
        }

        /// <summary>
        /// Gets a value indicating whether a widget can hold focus: it is not disabled
        /// (<see cref="IEnableable"/>), not hidden (<see cref="IHideable"/>), and, for a
        /// <see cref="FocusScope"/> or <see cref="FramedStack"/>, contains at least one focusable child (an empty
        /// scope is not focusable, so Tab no longer lands on it). Traversal additionally honors
        /// <see cref="IFocusStop"/>; see <see cref="IsTabStop"/>. Stateless and thread-safe.
        /// </summary>
        /// <param name="widget">The widget, or null.</param>
        /// <returns><c>true</c> when the widget is non-null, enabled, visible, and not an empty container.</returns>
        public static bool IsFocusable(object? widget)
        {
            if (widget == null)
                return false;

            if (widget is IHideable hideable && !hideable.IsVisible)
                return false;

            if (widget is IEnableable enableable && !enableable.IsEnabled)
                return false;

            return !(widget is IFocusChildren children) || children.HasFocusableChild;
        }

        /// <summary>
        /// Returns whether Tab and Shift+Tab stop on a widget: it must be focusable (see
        /// <see cref="IsFocusable"/>), must not opt out through <see cref="IFocusStop"/>, and, for a
        /// <see cref="FocusScope"/> or <see cref="FramedStack"/>, must contain a child that is itself a
        /// tab stop. Focus traversal and the host's region ring use this; <see cref="SetFocus(IFocusable)"/> and <see cref="RepairFocus"/> accept any
        /// focusable widget. Stateless and thread-safe.
        /// </summary>
        /// <param name="widget">The widget, or null.</param>
        /// <returns><c>true</c> when traversal stops on the widget; <c>false</c> for null.</returns>
        public static bool IsTabStop(object? widget)
        {
            if (!IsFocusable(widget))
                return false;

            if (widget is IFocusStop stop && !stop.IsFocusStop)
                return false;

            return !(widget is IFocusChildren children) || children.HasTabStopChild;
        }

        /// <summary>
        /// Moves focus off a focused child that can no longer hold it (hidden, disabled, removed, or an
        /// empty container) to the nearest tab-stop sibling, searching forward and then backward, and raises
        /// <see cref="FocusMoved"/>. A child entered this way that is a container gets its first (or, when
        /// found backward, last) child focused. When no sibling can take focus, focus leaves the scope:
        /// <see cref="FocusedIndex"/> becomes -1, <see cref="FocusedChild"/> null, and
        /// <see cref="FocusMoved"/> is raised with null; the scope then reports itself as not focusable,
        /// so its parent's repair moves on. Does nothing when nothing is focused or the focused child is
        /// fine. The host calls it for every scope on the focus path after each layout while
        /// <see cref="AutoRepair"/> is true; call it directly after hiding or disabling a child yourself.
        /// </summary>
        /// <returns><c>true</c> when focus moved; otherwise <c>false</c>.</returns>
        public bool RepairFocus()
        {
            if (_Index < 0)
                return false;

            if (_Index < _Children.Count && IsFocusable(_Children[_Index]))
                return false;

            int start = _Index;
            int next = FindFrom(start + 1, 1, false);
            bool forward = true;
            if (next < 0)
            {
                next = FindFrom(Math.Min(start - 1, _Children.Count - 1), -1, false);
                forward = false;
            }

            SetIndex(next);
            if (next >= 0 && _Children[next] is IFocusContainer container)
                container.FocusEdge(forward);

            return true;
        }

        bool IFocusChildren.HasFocusableChild
        {
            get
            {
                for (int i = 0; i < _Children.Count; i++)
                {
                    if (IsFocusable(_Children[i]))
                        return true;
                }

                return false;
            }
        }

        bool IFocusChildren.HasTabStopChild
        {
            get { return FindFrom(0, 1, true) >= 0; }
        }

        // The first child added takes focus as in 1.4.0 (visible and enabled), except a non-stop child.
        // An empty container still qualifies here, so scopes can be built top-down and filled afterwards;
        // repair moves focus on if it is still empty when a frame is drawn.
        private static bool IsAddCandidate(object child)
        {
            if (child is IHideable hideable && !hideable.IsVisible)
                return false;
            if (child is IEnableable enableable && !enableable.IsEnabled)
                return false;

            return !(child is IFocusStop stop && !stop.IsFocusStop);
        }

        private int FindFrom(int start, int step, bool allowCurrent)
        {
            for (int i = start; i >= 0 && i < _Children.Count; i += step)
            {
                if (!allowCurrent && i == _Index)
                    continue;

                if (IsTabStop(_Children[i]))
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

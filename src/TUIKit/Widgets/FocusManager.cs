namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit.Input;

    /// <summary>
    /// Tracks keyboard focus across a set of focusable widgets and routes input to the focused one.
    /// Tab moves to the next widget, Shift+Tab to the previous; other keys go to the current widget.
    /// Used by forms and any multi-widget screen that needs a focus ring. Whenever focus moves, widgets
    /// that implement <see cref="IFocusAware"/> are notified through <see cref="IFocusAware.OnFocusChanged"/>
    /// so their rendered focus state (caret, highlight) follows the routing. Traversal skips disabled
    /// widgets (<see cref="IEnableable"/>) and descends into <see cref="IFocusContainer"/> widgets: Tab
    /// first moves focus within a focused container and only moves on once the container reports that
    /// it is at its edge; entering a container focuses its first (or, backward, its last) descendant.
    /// </summary>
    public sealed class FocusManager
    {
        private readonly List<IFocusable> _Widgets = new List<IFocusable>();
        private int _Index;

        /// <summary>
        /// Gets or sets a value indicating whether Tab wraps from the last widget to the first (and
        /// Shift+Tab from the first to the last). Defaults to true, the original behavior. Set it to false
        /// when the ring is nested inside another focus scope: <see cref="HandleKey"/> then returns
        /// <c>false</c> for a Tab at either end so the parent moves focus on.
        /// </summary>
        public bool Wrap { get; set; } = true;

        /// <summary>
        /// Gets or sets an optional predicate deciding whether a registered widget can take focus by
        /// traversal (for example to skip hidden form rows). Disabled widgets (<see cref="IEnableable"/>)
        /// are always skipped. Null (the default) admits every enabled widget.
        /// </summary>
        public Func<IFocusable, bool>? CanFocus { get; set; }

        /// <summary>
        /// Gets the number of registered widgets.
        /// </summary>
        public int Count
        {
            get { return _Widgets.Count; }
        }

        /// <summary>
        /// Gets the zero-based index of the focused widget, or -1 when none are registered.
        /// </summary>
        public int FocusedIndex
        {
            get { return _Widgets.Count == 0 ? -1 : _Index; }
        }

        /// <summary>
        /// Gets the focused widget, or null when none are registered.
        /// </summary>
        public IFocusable? Focused
        {
            get { return _Widgets.Count == 0 ? null : _Widgets[_Index]; }
        }

        /// <summary>
        /// Registers focusable widgets in tab order.
        /// </summary>
        /// <param name="widgets">The widgets. Must not be null or contain nulls.</param>
        /// <exception cref="ArgumentNullException">Thrown when the array or an element is null.</exception>
        public void Register(params IFocusable[] widgets)
        {
            if (widgets == null)
                throw new ArgumentNullException(nameof(widgets));

            bool wasEmpty = _Widgets.Count == 0;
            for (int i = 0; i < widgets.Length; i++)
            {
                if (widgets[i] == null)
                    throw new ArgumentNullException(nameof(widgets));

                _Widgets.Add(widgets[i]);
            }

            if (wasEmpty && _Widgets.Count > 0)
            {
                _Index = 0;
                NotifyFocus(_Widgets[0], true);
            }
        }

        /// <summary>
        /// Removes every registered widget and resets focus, so a screen can rebuild its focus ring at
        /// runtime (for example when a form swaps its field set).
        /// </summary>
        public void Clear()
        {
            _Widgets.Clear();
            _Index = 0;
        }

        /// <summary>
        /// Moves focus to the next widget, wrapping around.
        /// </summary>
        public void Next()
        {
            Step(1);
        }

        /// <summary>
        /// Moves focus to the previous widget, wrapping around.
        /// </summary>
        public void Previous()
        {
            Step(-1);
        }

        /// <summary>
        /// Moves focus forward or backward, first within a focused <see cref="IFocusContainer"/>, then to
        /// the next eligible widget. Honors <see cref="Wrap"/>.
        /// </summary>
        /// <param name="forward"><c>true</c> to move forward; <c>false</c> to move backward.</param>
        /// <returns><c>true</c> when focus moved; <c>false</c> at an edge with <see cref="Wrap"/> off, or when nothing can take focus.</returns>
        public bool MoveFocus(bool forward)
        {
            if (Focused is IFocusContainer container && Eligible(container) && container.MoveFocus(forward))
                return true;

            int direction = forward ? 1 : -1;
            for (int candidate = _Index + direction; candidate >= 0 && candidate < _Widgets.Count; candidate += direction)
            {
                if (Eligible(_Widgets[candidate]))
                {
                    Enter(candidate, forward);
                    return true;
                }
            }

            if (!Wrap)
                return false;

            for (int candidate = forward ? 0 : _Widgets.Count - 1; candidate >= 0 && candidate < _Widgets.Count && candidate != _Index; candidate += direction)
            {
                if (Eligible(_Widgets[candidate]))
                {
                    Enter(candidate, forward);
                    return true;
                }
            }

            if (Focused is IFocusContainer only && Eligible(only))
            {
                only.FocusEdge(forward);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Focuses the first (or last) eligible widget, descending into a container.
        /// </summary>
        /// <param name="first"><c>true</c> for the first widget; <c>false</c> for the last.</param>
        public void FocusEdge(bool first)
        {
            int direction = first ? 1 : -1;
            for (int candidate = first ? 0 : _Widgets.Count - 1; candidate >= 0 && candidate < _Widgets.Count; candidate += direction)
            {
                if (Eligible(_Widgets[candidate]))
                {
                    Enter(candidate, first);
                    return;
                }
            }
        }

        private void Enter(int index, bool forward)
        {
            SetFocus(index);
            if (_Widgets[index] is IFocusContainer container)
                container.FocusEdge(forward);
        }

        private bool Eligible(IFocusable widget)
        {
            if (!FocusScope.IsFocusable(widget))
                return false;

            Func<IFocusable, bool>? predicate = CanFocus;
            return predicate == null || predicate(widget);
        }

        private void Step(int direction)
        {
            if (_Widgets.Count == 0)
                return;

            for (int offset = 1; offset <= _Widgets.Count; offset++)
            {
                int candidate = ((_Index + (direction * offset)) % _Widgets.Count + _Widgets.Count) % _Widgets.Count;
                if (candidate == _Index && _Widgets.Count > 1)
                    continue;
                if (!Eligible(_Widgets[candidate]))
                    continue;

                Enter(candidate, direction > 0);
                return;
            }
        }

        /// <summary>
        /// Moves focus to the widget at the supplied index, notifying both the widget that loses focus
        /// and the one that gains it.
        /// </summary>
        /// <param name="index">The zero-based index of the widget to focus. Must be within [0, Count).</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is out of range.</exception>
        public void SetFocus(int index)
        {
            if (index < 0 || index >= _Widgets.Count)
                throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be within [0, Count).");

            if (index == _Index)
                return;

            NotifyFocus(_Widgets[_Index], false);
            _Index = index;
            NotifyFocus(_Widgets[_Index], true);
        }

        private static void NotifyFocus(IFocusable widget, bool focused)
        {
            if (widget is IFocusAware aware)
                aware.OnFocusChanged(focused);
        }

        /// <summary>
        /// Routes a key: Tab/Shift+Tab move focus, everything else goes to the focused widget.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns><c>true</c> when the key was consumed; otherwise <c>false</c>.</returns>
        public bool HandleKey(KeyEvent key)
        {
            if (key.Code == KeyCode.Tab)
            {
                bool forward = (key.Modifiers & KeyModifiers.Shift) == 0;
                if (!Wrap)
                    return MoveFocus(forward);

                if (Focused is IFocusContainer container && Eligible(container) && container.MoveFocus(forward))
                    return true;

                if (forward)
                    Next();
                else
                    Previous();

                return true;
            }

            IFocusable? focused = Focused;
            return focused != null && focused.HandleKey(key);
        }
    }
}

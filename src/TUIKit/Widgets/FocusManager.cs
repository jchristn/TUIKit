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
    /// so their rendered focus state (caret, highlight) follows the routing.
    /// </summary>
    public sealed class FocusManager
    {
        private readonly List<IFocusable> _Widgets = new List<IFocusable>();
        private int _Index;

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
            if (_Widgets.Count > 1)
                SetFocus((_Index + 1) % _Widgets.Count);
        }

        /// <summary>
        /// Moves focus to the previous widget, wrapping around.
        /// </summary>
        public void Previous()
        {
            if (_Widgets.Count > 1)
                SetFocus((_Index - 1 + _Widgets.Count) % _Widgets.Count);
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
                if ((key.Modifiers & KeyModifiers.Shift) != 0)
                    Previous();
                else
                    Next();

                return true;
            }

            IFocusable? focused = Focused;
            return focused != null && focused.HandleKey(key);
        }
    }
}

namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit.Input;

    /// <summary>
    /// Tracks keyboard focus across a set of focusable widgets and routes input to the focused one.
    /// Tab moves to the next widget, Shift+Tab to the previous; other keys go to the current widget.
    /// Used by forms and any multi-widget screen that needs a focus ring.
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

            for (int i = 0; i < widgets.Length; i++)
            {
                if (widgets[i] == null)
                    throw new ArgumentNullException(nameof(widgets));

                _Widgets.Add(widgets[i]);
            }
        }

        /// <summary>
        /// Moves focus to the next widget, wrapping around.
        /// </summary>
        public void Next()
        {
            if (_Widgets.Count > 0)
                _Index = (_Index + 1) % _Widgets.Count;
        }

        /// <summary>
        /// Moves focus to the previous widget, wrapping around.
        /// </summary>
        public void Previous()
        {
            if (_Widgets.Count > 0)
                _Index = (_Index - 1 + _Widgets.Count) % _Widgets.Count;
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

namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Theming;

    /// <summary>
    /// A row of <see cref="Button"/>s that wraps onto further lines when the available width is too
    /// narrow. Left and Right move between buttons; Tab and Shift+Tab also move and bubble out at either
    /// end (it is an <see cref="IFocusContainer"/>); Enter or Space activates the focused button.
    /// </summary>
    /// <remarks>Not thread-safe: use it from the UI loop.</remarks>
    public sealed class ButtonRow : IWidget, IFocusContainer, IFocusAware, IMouseAware, IThemeable, ITooltipProvider
    {
        private readonly List<Button> _Buttons = new List<Button>();
        private readonly List<Rect> _Rects = new List<Rect>();
        private int _Focused;
        private bool _HasFocus;
        private int _Spacing = 1;
        private int _Hovered = -1;

        /// <summary>
        /// Gets or sets the gap between buttons in columns. Defaults to 1. Must be zero or greater.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when negative.</exception>
        public int Spacing
        {
            get { return _Spacing; }
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Spacing must be zero or greater.");
                _Spacing = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether buttons wrap onto further lines when they do not fit.
        /// Defaults to true; when false, buttons that do not fit are not drawn.
        /// </summary>
        public bool Wrap { get; set; } = true;

        /// <summary>
        /// Gets the buttons in order. Never null.
        /// </summary>
        public IReadOnlyList<Button> Buttons
        {
            get { return _Buttons; }
        }

        /// <summary>
        /// Gets the index of the focused button, or -1 when there are none.
        /// </summary>
        public int FocusedIndex
        {
            get { return _Buttons.Count == 0 ? -1 : _Focused; }
        }

        /// <inheritdoc/>
        public IFocusable? FocusedLeaf
        {
            get { return _Buttons.Count == 0 ? null : _Buttons[_Focused]; }
        }

        /// <summary>
        /// Adds a button.
        /// </summary>
        /// <param name="button">The button. Must not be null.</param>
        /// <returns>The button.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="button"/> is null.</exception>
        public Button Add(Button button)
        {
            if (button == null)
                throw new ArgumentNullException(nameof(button));

            _Buttons.Add(button);
            button.OnFocusChanged(_HasFocus && _Buttons.Count - 1 == _Focused);
            return button;
        }

        /// <summary>
        /// Adds a button with a label and click handler.
        /// </summary>
        /// <param name="label">The label. Must not be null.</param>
        /// <param name="onClick">The click handler, or null.</param>
        /// <returns>The button.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="label"/> is null.</exception>
        public Button Add(string label, Action? onClick = null)
        {
            return Add(new Button(label, onClick));
        }

        /// <summary>
        /// Removes every button.
        /// </summary>
        public void Clear()
        {
            _Buttons.Clear();
            _Rects.Clear();
            _Focused = 0;
        }

        /// <summary>
        /// Computes the number of lines the buttons need at a width.
        /// </summary>
        /// <param name="width">The available width.</param>
        /// <returns>The line count; at least 1 when there are buttons, 0 otherwise.</returns>
        public int LineCount(int width)
        {
            List<Rect> rects = Layout(width);
            int lines = 0;
            for (int i = 0; i < rects.Count; i++)
                lines = Math.Max(lines, rects[i].Y + 1);

            return lines;
        }

        /// <inheritdoc/>
        public bool HandleKey(KeyEvent key)
        {
            if (_Buttons.Count == 0)
                return false;

            if (key.Modifiers == KeyModifiers.None && key.Code == KeyCode.Right)
                return Step(1);
            if (key.Modifiers == KeyModifiers.None && key.Code == KeyCode.Left)
                return Step(-1);
            if (key.Code == KeyCode.Tab && (key.Modifiers & ~KeyModifiers.Shift) == KeyModifiers.None)
                return MoveFocus((key.Modifiers & KeyModifiers.Shift) == 0);

            return _Buttons[_Focused].HandleKey(key);
        }

        /// <inheritdoc/>
        public bool MoveFocus(bool forward)
        {
            return Step(forward ? 1 : -1);
        }

        /// <inheritdoc/>
        public void FocusEdge(bool first)
        {
            int direction = first ? 1 : -1;
            for (int i = first ? 0 : _Buttons.Count - 1; i >= 0 && i < _Buttons.Count; i += direction)
            {
                if (_Buttons[i].IsEnabled)
                {
                    SetFocused(i);
                    return;
                }
            }
        }

        /// <inheritdoc/>
        public void OnFocusChanged(bool focused)
        {
            _HasFocus = focused;
            if (_Buttons.Count > 0)
                _Buttons[_Focused].OnFocusChanged(focused);
        }

        /// <summary>
        /// Applies a theme to every button.
        /// </summary>
        /// <param name="theme">The theme. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
        public void ApplyTheme(Theme theme)
        {
            if (theme == null)
                throw new ArgumentNullException(nameof(theme));

            for (int i = 0; i < _Buttons.Count; i++)
                _Buttons[i].ApplyTheme(theme);
        }

        /// <inheritdoc/>
        public string? GetTooltip(int x, int y)
        {
            int index = IndexAt(x, y);
            return index >= 0 ? _Buttons[index].TooltipText : null;
        }

        /// <summary>
        /// Routes the mouse to the button under the pointer; a press also focuses it.
        /// </summary>
        /// <param name="mouse">The mouse event in widget-local coordinates. Must not be null.</param>
        /// <returns><c>true</c> when a button consumed the event.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mouse"/> is null.</exception>
        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            int index = mouse.Kind == MouseEventKind.Leave ? -1 : IndexAt(mouse.X, mouse.Y);
            if (index != _Hovered)
            {
                if (_Hovered >= 0 && _Hovered < _Buttons.Count)
                    _Buttons[_Hovered].HandleMouse(new MouseEvent(MouseEventKind.Leave, MouseButton.None, 0, 0, mouse.Modifiers, 0));
                if (index >= 0)
                    _Buttons[index].HandleMouse(new MouseEvent(MouseEventKind.Enter, MouseButton.None, 0, 0, mouse.Modifiers, 0));
                _Hovered = index;
            }

            if (index < 0)
                return false;

            if (mouse.Kind == MouseEventKind.Press && _Buttons[index].IsEnabled)
                SetFocused(index);

            Rect rect = _Rects[index];
            return _Buttons[index].HandleMouse(new MouseEvent(mouse.Kind, mouse.Button, mouse.X - rect.X, mouse.Y - rect.Y, mouse.Modifiers, mouse.ClickCount));
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            return new Size(available.Width, Math.Min(available.Height, LineCount(available.Width)));
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            _Rects.Clear();
            _Rects.AddRange(Layout(surface.Size.Width));
            for (int i = 0; i < _Buttons.Count; i++)
            {
                Rect rect = _Rects[i];
                if (rect.Width <= 0 || rect.Y >= surface.Size.Height)
                    continue;

                _Buttons[i].Render(new SurfaceView(surface, rect));
            }
        }

        private List<Rect> Layout(int width)
        {
            List<Rect> rects = new List<Rect>(_Buttons.Count);
            int x = 0;
            int y = 0;
            for (int i = 0; i < _Buttons.Count; i++)
            {
                int w = Math.Min(_Buttons[i].Width, Math.Max(1, width));
                if (x > 0 && x + w > width)
                {
                    if (!Wrap)
                    {
                        rects.Add(new Rect(0, 0, 0, 0));
                        continue;
                    }

                    x = 0;
                    y++;
                }

                rects.Add(new Rect(x, y, w, 1));
                x += w + _Spacing;
            }

            return rects;
        }

        private int IndexAt(int x, int y)
        {
            for (int i = 0; i < _Rects.Count && i < _Buttons.Count; i++)
            {
                if (_Rects[i].Width > 0 && _Rects[i].Contains(new Point(x, y)))
                    return i;
            }

            return -1;
        }

        private bool Step(int direction)
        {
            for (int i = _Focused + direction; i >= 0 && i < _Buttons.Count; i += direction)
            {
                if (_Buttons[i].IsEnabled)
                {
                    SetFocused(i);
                    return true;
                }
            }

            return false;
        }

        private void SetFocused(int index)
        {
            if (index == _Focused)
                return;

            if (_Focused < _Buttons.Count)
                _Buttons[_Focused].OnFocusChanged(false);
            _Focused = index;
            _Buttons[index].OnFocusChanged(_HasFocus);
        }
    }
}

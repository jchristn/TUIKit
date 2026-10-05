namespace TUIKit.Widgets
{
    using System;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Theming;

    /// <summary>
    /// A collapsible section: a header line with a disclosure marker over a child widget that is shown
    /// only while expanded. Enter or Space on the header toggles it, as does a click on the header.
    /// </summary>
    /// <remarks>
    /// With <see cref="ForwardKeys"/> set the section is a hierarchical focus scope
    /// (<see cref="IFocusContainer"/>) with two stops, the header and (while expanded) the child: Tab moves
    /// from the header into the child and bubbles out after it; on the header, Enter/Space toggle, Right
    /// expands and Left collapses; while the child has focus it receives every key first. Not
    /// thread-safe: use it from the UI loop.
    /// </remarks>
    public sealed class Collapsible : IWidget, IFocusable, IMouseAware, IFocusContainer, IFocusAware, IThemeable, IFocusPathNode
    {
        private readonly IWidget _Child;
        private string _Header;
        private bool _Expanded = true;
        private bool _ChildFocused;
        private bool _Focused;

        /// <summary>
        /// Raised after <see cref="Expanded"/> changes, from a key, a click, or a programmatic set.
        /// </summary>
        public event EventHandler<ValueChangedEventArgs<bool>>? ExpandedChanged;

        /// <summary>
        /// Gets or sets a value indicating whether the child is shown. Defaults to true. Collapsing while the
        /// child has focus moves focus back to the header.
        /// </summary>
        public bool Expanded
        {
            get { return _Expanded; }
            set
            {
                if (_Expanded == value)
                    return;

                _Expanded = value;
                if (!value && _ChildFocused)
                    SetChildFocused(false);

                ExpandedChanged?.Invoke(this, new ValueChangedEventArgs<bool>(!value, value));
            }
        }

        /// <summary>
        /// Gets or sets the header text. Must not be null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string Header
        {
            get { return _Header; }
            set { _Header = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>
        /// Gets or sets the header style. Defaults to bold.
        /// </summary>
        public CellStyle HeaderStyle { get; set; } = CellStyle.Default.WithAttribute(CellAttributes.Bold, true);

        /// <summary>
        /// Gets or sets the style composed over <see cref="HeaderStyle"/> while the header itself has focus.
        /// Defaults to <see cref="CellStyle.Default"/> (no visible change), preserving the original look.
        /// </summary>
        public CellStyle FocusedHeaderStyle { get; set; } = CellStyle.Default;

        /// <summary>
        /// Gets or sets the marker drawn before the header while expanded. Defaults to a down-pointing
        /// triangle followed by a space.
        /// </summary>
        public string ExpandedMarker { get; set; } = "\u25BC ";

        /// <summary>
        /// Gets or sets the marker drawn before the header while collapsed. Defaults to a right-pointing
        /// triangle followed by a space.
        /// </summary>
        public string CollapsedMarker { get; set; } = "\u25B6 ";

        /// <summary>
        /// Gets or sets a value indicating whether the section acts as a focus scope over its child (see
        /// the class remarks). Defaults to false, which keeps the original behavior (the header handles
        /// every key and the child never sees keys).
        /// </summary>
        public bool ForwardKeys { get; set; }

        /// <summary>
        /// Gets the child widget.
        /// </summary>
        public IWidget Child
        {
            get { return _Child; }
        }

        /// <summary>
        /// Gets the child that holds focus one level down, or null when focus rests on the header. Part of
        /// <see cref="IFocusPathNode"/>; the host uses it to build <see cref="FocusPath"/>.
        /// </summary>
        public IFocusable? FocusedChild
        {
            get { return _ChildFocused ? _Child as IFocusable : null; }
        }

        /// <summary>
        /// Gets a value indicating whether the child (rather than the header) has focus within the section.
        /// </summary>
        public bool ChildFocused
        {
            get { return _ChildFocused; }
        }

        /// <inheritdoc/>
        public IFocusable? FocusedLeaf
        {
            get
            {
                if (!_ChildFocused)
                    return this;

                if (_Child is IFocusContainer container)
                    return container.FocusedLeaf ?? container;

                return _Child as IFocusable ?? this;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Collapsible"/> class.
        /// </summary>
        /// <param name="header">The header text. Must not be null.</param>
        /// <param name="child">The child widget. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public Collapsible(string header, IWidget child)
        {
            _Header = header ?? throw new ArgumentNullException(nameof(header));
            _Child = child ?? throw new ArgumentNullException(nameof(child));
        }

        /// <summary>
        /// Toggles <see cref="Expanded"/>.
        /// </summary>
        public void Toggle()
        {
            Expanded = !Expanded;
        }

        /// <summary>
        /// Toggles on Enter or Space. With <see cref="ForwardKeys"/> set, see the class remarks.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns><c>true</c> when the key was consumed; otherwise <c>false</c>.</returns>
        public bool HandleKey(KeyEvent key)
        {
            if (ForwardKeys)
            {
                if (_ChildFocused && _Child is IFocusable focusable && focusable.HandleKey(key))
                    return true;

                if (key.Code == KeyCode.Tab && (key.Modifiers & ~KeyModifiers.Shift) == KeyModifiers.None)
                    return MoveFocus((key.Modifiers & KeyModifiers.Shift) == 0);

                if (_ChildFocused)
                    return false;

                if (key.Code == KeyCode.Right && key.Modifiers == KeyModifiers.None && !_Expanded)
                {
                    Expanded = true;
                    return true;
                }

                if (key.Code == KeyCode.Left && key.Modifiers == KeyModifiers.None && _Expanded)
                {
                    Expanded = false;
                    return true;
                }
            }

            if (key.Code == KeyCode.Enter || (key.Code == KeyCode.Character && key.Rune == ' '))
            {
                Toggle();
                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        public bool MoveFocus(bool forward)
        {
            if (!ForwardKeys)
                return false;

            if (_ChildFocused)
            {
                if (_Child is IFocusContainer container && container.MoveFocus(forward))
                    return true;

                if (!forward)
                {
                    SetChildFocused(false);
                    return true;
                }

                return false;
            }

            if (forward && ChildCanFocus())
            {
                SetChildFocused(true);
                if (_Child is IFocusContainer entered)
                    entered.FocusEdge(true);
                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        public void FocusEdge(bool first)
        {
            if (!ForwardKeys)
                return;

            if (!first && ChildCanFocus())
            {
                SetChildFocused(true);
                if (_Child is IFocusContainer container)
                    container.FocusEdge(false);
            }
            else
            {
                SetChildFocused(false);
            }
        }

        /// <summary>
        /// Tracks focus and, while the child has focus, forwards it. Part of <see cref="IFocusAware"/>.
        /// </summary>
        /// <param name="focused"><c>true</c> when the section gained focus; otherwise <c>false</c>.</param>
        public void OnFocusChanged(bool focused)
        {
            _Focused = focused;
            if (_ChildFocused && _Child is IFocusAware aware)
                aware.OnFocusChanged(focused);
        }

        /// <summary>
        /// Applies a theme: <see cref="HeaderStyle"/> from <see cref="Theme.Accent"/> (bold),
        /// <see cref="FocusedHeaderStyle"/> from <see cref="Theme.Selection"/>, then forwards it to the child.
        /// </summary>
        /// <param name="theme">The theme. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
        public void ApplyTheme(Theme theme)
        {
            if (theme == null)
                throw new ArgumentNullException(nameof(theme));

            HeaderStyle = theme.Accent.WithAttribute(CellAttributes.Bold, true);
            FocusedHeaderStyle = theme.Selection;
            ThemeApplier.Apply(_Child, theme);
        }

        /// <summary>
        /// Toggles on a left press on the header and forwards other events over the child (while
        /// expanded) to it when it is mouse-aware.
        /// </summary>
        /// <param name="mouse">The mouse event in widget-local coordinates. Must not be null.</param>
        /// <returns><c>true</c> when the event was consumed; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mouse"/> is null.</exception>
        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            if (mouse.Y == 0)
            {
                if (mouse.Kind == MouseEventKind.Press && mouse.Button == MouseButton.Left)
                {
                    if (ForwardKeys)
                        SetChildFocused(false);
                    Toggle();
                    return true;
                }

                return false;
            }

            if (_Expanded && mouse.Y >= 1)
            {
                if (ForwardKeys && mouse.Kind == MouseEventKind.Press && ChildCanFocus())
                    SetChildFocused(true);

                if (_Child is IMouseAware aware)
                    return aware.HandleMouse(new MouseEvent(mouse.Kind, mouse.Button, mouse.X, mouse.Y - 1, mouse.Modifiers, mouse.ClickCount));
            }

            return false;
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            int height = 1;
            if (_Expanded && available.Height > 1)
                height += _Child.Measure(new Size(available.Width, available.Height - 1)).Height;

            return new Size(available.Width, Math.Min(available.Height, height));
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            string disclosure = _Expanded ? ExpandedMarker : CollapsedMarker;
            CellStyle style = ForwardKeys && _Focused && !_ChildFocused ? FocusedHeaderStyle.Over(HeaderStyle) : HeaderStyle;
            surface.DrawText(0, 0, disclosure + _Header, style);

            if (_Expanded && surface.Size.Height > 1)
                _Child.Render(new SurfaceView(surface, new Rect(0, 1, surface.Size.Width, surface.Size.Height - 1)));
        }

        private bool ChildCanFocus()
        {
            return _Expanded && _Child is IFocusable && FocusScope.IsFocusable(_Child);
        }

        private void SetChildFocused(bool childFocused)
        {
            if (_ChildFocused == childFocused)
                return;

            _ChildFocused = childFocused;
            if (_Child is IFocusAware aware)
                aware.OnFocusChanged(childFocused && _Focused);
        }
    }
}

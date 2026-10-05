namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Theming;

    /// <summary>
    /// Composes labeled input widgets into a form with a shared tab order and optional per-field
    /// validation. Tab and Shift+Tab move between fields; other keys go to the focused field. The field
    /// set can be rebuilt at runtime with <see cref="Clear"/> and <see cref="Add{TWidget}"/>, which is
    /// how a dependent form swaps its fields when a selection changes. The form implements
    /// <see cref="IScrollExtent"/>, so placing it inside a <see cref="ScrollView"/> scrolls the focused
    /// field into view automatically when the form is taller than the viewport.
    /// </summary>
    /// <remarks>
    /// Fields can be hidden (<see cref="SetFieldVisible"/>), which removes them from layout and focus
    /// traversal without rebuilding the form, and disabled (<see cref="SetFieldEnabled"/>), which skips
    /// them in traversal and dims them. Fields whose widgets implement <see cref="IChangeNotifier"/> drive
    /// dirty tracking (<see cref="IsDirty"/>, <see cref="DirtyChanged"/>, <see cref="MarkClean"/>), which
    /// can be turned off with <see cref="TrackChanges"/>. The form is a hierarchical focus scope
    /// (<see cref="IFocusContainer"/>); set <see cref="WrapFocus"/> to false when it is nested so Tab at
    /// the last field moves on to the next sibling. Not thread-safe: use it from the UI loop.
    /// </remarks>
    public sealed class Form : IWidget, IFocusable, IScrollExtent, IMouseAware, IFocusContainer, IFocusAware, IThemeable, IFocusPathNode
    {
        private readonly List<FormField> _Fields = new List<FormField>();
        private readonly FocusManager _Focus = new FocusManager();
        private readonly List<int> _FieldTops = new List<int>();
        private readonly List<int> _FieldHeights = new List<int>();
        private readonly List<Rect> _FieldWidgetRects = new List<Rect>();
        private readonly List<int> _RenderedIndexes = new List<int>();
        private int _ContentHeight;
        private int _ContentWidth;
        private bool _Dirty;
        private bool _TrackChanges = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="Form"/> class.
        /// </summary>
        public Form()
        {
            _Focus.CanFocus = CanFocusField;
        }

        /// <summary>
        /// Raised when <see cref="IsDirty"/> changes: on the first change after the form was clean, and
        /// on <see cref="MarkClean"/>. The argument is the new dirty state.
        /// </summary>
        public event Action<bool>? DirtyChanged;

        /// <summary>
        /// Gets the number of fields, including hidden ones.
        /// </summary>
        public int FieldCount
        {
            get { return _Fields.Count; }
        }

        /// <summary>
        /// Gets the zero-based index of the focused field, or -1 when the form is empty.
        /// </summary>
        public int FocusedIndex
        {
            get { return _Focus.FocusedIndex; }
        }

        /// <summary>
        /// Gets or sets the style of field labels. Defaults to a grey (palette 8) foreground.
        /// </summary>
        public CellStyle LabelStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(8));

        /// <summary>
        /// Gets or sets the style of the focused field's label. Defaults to a cyan (palette 6) foreground.
        /// </summary>
        public CellStyle FocusedLabelStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(6));

        /// <summary>
        /// Gets or sets the style composed over the label of a disabled field. Defaults to dim text.
        /// </summary>
        public CellStyle DisabledLabelStyle { get; set; } = CellStyle.Default.WithAttribute(CellAttributes.Dim, true);

        /// <summary>
        /// Gets or sets a value indicating whether Tab wraps from the last field to the first. Defaults to
        /// true, the original behavior; set it to false when the form is nested in another focus scope.
        /// </summary>
        public bool WrapFocus
        {
            get { return _Focus.Wrap; }
            set { _Focus.Wrap = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether changes raised by field widgets mark the form dirty.
        /// Defaults to true. Turning it off also clears <see cref="IsDirty"/>.
        /// </summary>
        public bool TrackChanges
        {
            get { return _TrackChanges; }
            set
            {
                _TrackChanges = value;
                if (!value)
                    SetDirty(false);
            }
        }

        /// <summary>
        /// Gets the child that holds focus one level down, or null when the form has no focusable fields. Part of
        /// <see cref="IFocusPathNode"/>; the host uses it to build <see cref="FocusPath"/>.
        /// </summary>
        public IFocusable? FocusedChild
        {
            get { return _Focus.Focused; }
        }

        /// <summary>
        /// Gets a value indicating whether any field widget reported a change since the form was built or
        /// last marked clean. Only widgets implementing <see cref="IChangeNotifier"/> are tracked.
        /// </summary>
        public bool IsDirty
        {
            get { return _Dirty; }
        }

        /// <inheritdoc/>
        public IFocusable? FocusedLeaf
        {
            get
            {
                IFocusable? focused = _Focus.Focused;
                if (focused is IFocusContainer container)
                    return container.FocusedLeaf ?? container;

                return focused;
            }
        }

        /// <summary>
        /// Adds a labeled field.
        /// </summary>
        /// <typeparam name="TWidget">The widget type, which must be focusable.</typeparam>
        /// <param name="label">The field label. Must not be null. An empty label draws no label row.</param>
        /// <param name="widget">The input widget. Must not be null.</param>
        /// <param name="validator">An optional validator returning an error message, or null when valid.</param>
        /// <returns>The widget, for further configuration.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
        public TWidget Add<TWidget>(string label, TWidget widget, Func<string?>? validator = null)
            where TWidget : IWidget, IFocusable
        {
            if (label == null)
                throw new ArgumentNullException(nameof(label));
            if (widget == null)
                throw new ArgumentNullException(nameof(widget));

            FormField field = new FormField(label, widget, widget, validator);
            field.Inline = label.Length == 0;
            _Fields.Add(field);
            _Focus.Register(widget);
            if (widget is IChangeNotifier notifier)
                notifier.Changed += OnFieldChanged;
            return widget;
        }

        /// <summary>
        /// Adds a checkbox field whose own label is the field label, drawn on a single row.
        /// </summary>
        /// <param name="label">The checkbox label. Must not be null.</param>
        /// <param name="isChecked">The initial state. Defaults to false.</param>
        /// <returns>The checkbox.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="label"/> is null.</exception>
        public Checkbox AddCheckbox(string label, bool isChecked = false)
        {
            if (label == null)
                throw new ArgumentNullException(nameof(label));

            Checkbox box = Add(string.Empty, new Checkbox(label, isChecked));
            return box;
        }

        /// <summary>
        /// Gets the widget of a field.
        /// </summary>
        /// <param name="index">The zero-based field index. Must be within [0, FieldCount).</param>
        /// <returns>The field's widget.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is out of range.</exception>
        public IWidget GetField(int index)
        {
            CheckIndex(index);
            return _Fields[index].Widget;
        }

        /// <summary>
        /// Changes a field's label.
        /// </summary>
        /// <param name="index">The zero-based field index. Must be within [0, FieldCount).</param>
        /// <param name="label">The new label. Must not be null.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is out of range.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="label"/> is null.</exception>
        public void SetFieldLabel(int index, string label)
        {
            CheckIndex(index);
            _Fields[index].Label = label ?? throw new ArgumentNullException(nameof(label));
        }

        /// <summary>
        /// Shows or hides a field. A hidden field takes no space, is skipped by focus traversal and mouse
        /// routing, and is ignored by <see cref="Validate"/>. Hiding the focused field moves focus to the
        /// next visible field.
        /// </summary>
        /// <param name="index">The zero-based field index. Must be within [0, FieldCount).</param>
        /// <param name="visible"><c>true</c> to show the field; <c>false</c> to hide it.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is out of range.</exception>
        public void SetFieldVisible(int index, bool visible)
        {
            CheckIndex(index);
            _Fields[index].Visible = visible;
            if (!visible && _Focus.FocusedIndex == index && !_Focus.MoveFocus(true))
                _Focus.MoveFocus(false);
        }

        /// <summary>
        /// Gets a value indicating whether a field is visible.
        /// </summary>
        /// <param name="index">The zero-based field index. Must be within [0, FieldCount).</param>
        /// <returns><c>true</c> when visible.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is out of range.</exception>
        public bool IsFieldVisible(int index)
        {
            CheckIndex(index);
            return _Fields[index].Visible;
        }

        /// <summary>
        /// Enables or disables a field whose widget implements <see cref="IEnableable"/> (a no-op for other
        /// widgets). A disabled field is skipped by focus traversal and drawn dimmed.
        /// </summary>
        /// <param name="index">The zero-based field index. Must be within [0, FieldCount).</param>
        /// <param name="enabled"><c>true</c> to enable; <c>false</c> to disable.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is out of range.</exception>
        public void SetFieldEnabled(int index, bool enabled)
        {
            CheckIndex(index);
            if (_Fields[index].Widget is IEnableable enableable)
                enableable.IsEnabled = enabled;

            if (!enabled && _Focus.FocusedIndex == index && !_Focus.MoveFocus(true))
                _Focus.MoveFocus(false);
        }

        /// <summary>
        /// Clears <see cref="IsDirty"/>, for example after saving.
        /// </summary>
        public void MarkClean()
        {
            SetDirty(false);
        }

        /// <summary>
        /// Removes every field and resets the focus ring, so the form can be rebuilt with a different
        /// field set at runtime (for example when a dependent selection changes which fields apply).
        /// The form becomes clean.
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < _Fields.Count; i++)
            {
                if (_Fields[i].Widget is IChangeNotifier notifier)
                    notifier.Changed -= OnFieldChanged;
            }

            _Fields.Clear();
            _Focus.Clear();
            _FieldTops.Clear();
            _FieldHeights.Clear();
            _FieldWidgetRects.Clear();
            _RenderedIndexes.Clear();
            _ContentHeight = 0;
            SetDirty(false);
        }

        /// <summary>
        /// Moves focus to the field at the supplied index. Use it to restore a sensible focus after a
        /// rebuild.
        /// </summary>
        /// <param name="index">The zero-based field index. Must be within [0, FieldCount).</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is out of range.</exception>
        public void SetFocusedField(int index)
        {
            if (index < 0 || index >= _Fields.Count)
                throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be within [0, FieldCount).");

            _Focus.SetFocus(index);
        }

        /// <inheritdoc/>
        public int ContentHeight
        {
            get
            {
                if (_ContentHeight > 0)
                    return _ContentHeight;

                int total = 0;
                for (int i = 0; i < _Fields.Count; i++)
                {
                    if (_Fields[i].Visible)
                        total += _Fields[i].Inline ? 2 : 3;
                }

                return total;
            }
        }

        /// <inheritdoc/>
        public bool TryGetFocusRect(out Rect rect)
        {
            int index = _Focus.FocusedIndex;
            int slot = _RenderedIndexes.IndexOf(index);
            if (slot < 0 || slot >= _FieldTops.Count)
            {
                rect = default;
                return false;
            }

            rect = new Rect(0, _FieldTops[slot], Math.Max(1, _ContentWidth), _FieldHeights[slot]);
            return true;
        }

        /// <summary>
        /// Runs every visible field's validator and returns the first error message, or null when all
        /// visible fields are valid.
        /// </summary>
        /// <returns>The first validation error, or null.</returns>
        public string? Validate()
        {
            for (int i = 0; i < _Fields.Count; i++)
            {
                if (_Fields[i].Visible && _Fields[i].Validator != null)
                {
                    string? error = _Fields[i].Validator!();
                    if (error != null)
                        return error;
                }
            }

            return null;
        }

        /// <inheritdoc/>
        public bool HandleKey(KeyEvent key)
        {
            return _Focus.HandleKey(key);
        }

        /// <inheritdoc/>
        public bool MoveFocus(bool forward)
        {
            if (_Focus.Wrap)
            {
                // A wrapping ring never reports an edge, so nested use relies on WrapFocus being false.
                _Focus.MoveFocus(forward);
                return true;
            }

            return _Focus.MoveFocus(forward);
        }

        /// <inheritdoc/>
        public void FocusEdge(bool first)
        {
            _Focus.FocusEdge(first);
        }

        /// <summary>
        /// Forwards focus changes to the focused field. Part of <see cref="IFocusAware"/>.
        /// </summary>
        /// <param name="focused"><c>true</c> when the form gained focus; otherwise <c>false</c>.</param>
        public void OnFocusChanged(bool focused)
        {
            if (_Focus.Focused is IFocusAware aware)
                aware.OnFocusChanged(focused);
        }

        /// <summary>
        /// Applies a theme: <see cref="LabelStyle"/> from <see cref="Theme.Muted"/>,
        /// <see cref="FocusedLabelStyle"/> from <see cref="Theme.Accent"/>, <see cref="DisabledLabelStyle"/>
        /// from <see cref="Theme.Disabled"/>, then forwards it to every field widget.
        /// </summary>
        /// <param name="theme">The theme. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
        public void ApplyTheme(Theme theme)
        {
            if (theme == null)
                throw new ArgumentNullException(nameof(theme));

            LabelStyle = theme.Muted;
            FocusedLabelStyle = theme.Accent;
            DisabledLabelStyle = theme.Disabled;
            for (int i = 0; i < _Fields.Count; i++)
                ThemeApplier.Apply(_Fields[i].Widget, theme);
        }

        /// <summary>
        /// Routes a mouse event to the field under the pointer: a left press focuses that field, and the event
        /// is then forwarded (in field-widget-local coordinates) to the field's widget when it is itself
        /// mouse-aware, so a click lands the caret in the right text field, toggles the right checkbox, and so
        /// on. Coordinates are form-local. Returns false when no field is under the pointer. Disabled fields
        /// are not focused by a click.
        /// </summary>
        /// <param name="mouse">The mouse event in form-local coordinates. Must not be null.</param>
        /// <returns><c>true</c> when a field consumed the event; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mouse"/> is null.</exception>
        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            for (int slot = 0; slot < _FieldTops.Count; slot++)
            {
                if (mouse.Y < _FieldTops[slot] || mouse.Y >= _FieldTops[slot] + _FieldHeights[slot])
                    continue;

                int i = _RenderedIndexes[slot];
                if (mouse.Kind == MouseEventKind.Press && mouse.Button == MouseButton.Left && FocusScope.IsFocusable(_Fields[i].Widget))
                    _Focus.SetFocus(i);

                if (slot < _FieldWidgetRects.Count && _Fields[i].Widget is IMouseAware aware)
                {
                    Rect wr = _FieldWidgetRects[slot];
                    if (mouse.X >= wr.X && mouse.X < wr.X + wr.Width && mouse.Y >= wr.Y && mouse.Y < wr.Y + wr.Height)
                    {
                        aware.HandleMouse(new MouseEvent(mouse.Kind, mouse.Button, mouse.X - wr.X, mouse.Y - wr.Y, mouse.Modifiers, mouse.ClickCount));
                    }
                }

                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            return available;
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            int width = surface.Size.Width;
            int height = surface.Size.Height;
            if (width <= 0 || height <= 0)
                return;

            _FieldTops.Clear();
            _FieldHeights.Clear();
            _FieldWidgetRects.Clear();
            _RenderedIndexes.Clear();
            _ContentWidth = width;
            int y = 0;
            for (int i = 0; i < _Fields.Count && y < height; i++)
            {
                FormField field = _Fields[i];
                if (!field.Visible)
                    continue;

                bool focused = _Focus.FocusedIndex == i;
                bool enabled = FocusScope.IsFocusable(field.Widget);
                int blockTop = y;
                if (!field.Inline)
                {
                    CellStyle labelStyle = focused ? FocusedLabelStyle : LabelStyle;
                    if (!enabled)
                        labelStyle = DisabledLabelStyle.Over(labelStyle);
                    surface.DrawText(0, y, (focused ? "> " : "  ") + field.Label, labelStyle);
                    y++;
                }

                int fieldHeight = Math.Max(1, field.Widget.Measure(new Size(width - 2, Math.Max(0, height - y))).Height);
                Rect widgetRect = new Rect(2, y, width - 2, Math.Max(0, Math.Min(fieldHeight, height - y)));
                if (field.Inline && focused)
                    surface.DrawText(0, y, "> ", FocusedLabelStyle);
                if (y < height)
                    field.Widget.Render(new SurfaceView(surface, widgetRect));

                y += fieldHeight + 1;
                _FieldTops.Add(blockTop);
                _FieldHeights.Add(Math.Max(1, y - blockTop));
                _FieldWidgetRects.Add(widgetRect);
                _RenderedIndexes.Add(i);
            }

            _ContentHeight = y;
        }

        private bool CanFocusField(IFocusable widget)
        {
            for (int i = 0; i < _Fields.Count; i++)
            {
                if (ReferenceEquals(_Fields[i].Focusable, widget))
                    return _Fields[i].Visible;
            }

            return true;
        }

        private void OnFieldChanged(object? sender, EventArgs e)
        {
            if (_TrackChanges)
                SetDirty(true);
        }

        private void SetDirty(bool dirty)
        {
            if (_Dirty == dirty)
                return;

            _Dirty = dirty;
            DirtyChanged?.Invoke(dirty);
        }

        private void CheckIndex(int index)
        {
            if (index < 0 || index >= _Fields.Count)
                throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be within [0, FieldCount).");
        }
    }
}

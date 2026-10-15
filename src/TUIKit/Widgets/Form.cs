namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Theming;
    using TUIKit.Unicode;

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
    /// can be turned off with <see cref="TrackChanges"/>; <see cref="DirtyTracking"/> set to
    /// <see cref="FormDirtyTracking.Snapshot"/> compares values instead, so a revert clears it. Opt-in
    /// features cover create and settings dialogs: section headings (<see cref="AddSection"/>), hints
    /// (<see cref="SetFieldHint"/>), errors drawn under each field (<see cref="ShowFieldErrors"/>,
    /// <see cref="ValidateAll"/>), submit and cancel (<see cref="Submit"/>, <see cref="Cancel"/>,
    /// <see cref="SubmitKey"/>, <see cref="ShowButtons"/>), labels beside fields
    /// (<see cref="LabelPlacement"/>), and arrow keys between fields (<see cref="ArrowKeysMoveFocus"/>). The
    /// form is a hierarchical focus scope (<see cref="IFocusContainer"/>); set <see cref="WrapFocus"/> to false
    /// when it is nested so Tab at the last field moves on to the next sibling. Not thread-safe: use it from
    /// the UI loop.
    /// </remarks>
    public sealed class Form : IWidget, IFocusable, IScrollExtent, IMouseAware, IFocusContainer, IFocusAware, IThemeable, IFocusPathNode, IPasteTarget, IKeyHintSource
    {
        #region Private-Members

        private readonly List<FormField> _Fields = new List<FormField>();
        private readonly List<FormSection> _Sections = new List<FormSection>();
        private readonly FocusManager _Focus = new FocusManager();
        private readonly List<int> _FieldTops = new List<int>();
        private readonly List<int> _FieldHeights = new List<int>();
        private readonly List<Rect> _FieldWidgetRects = new List<Rect>();
        private readonly List<int> _RenderedIndexes = new List<int>();
        private int _ContentHeight;
        private int _ContentWidth;
        private bool _Dirty;
        private bool _TrackChanges = true;
        private bool _ShowButtons;
        private ButtonRow? _Buttons;
        private Button? _SubmitButton;
        private Button? _CancelButton;
        private string _SubmitLabel = "Save";
        private string _CancelLabel = "Cancel";
        private string _ErrorPrefix = "! ";
        private int _LabelWidth;
        private Rect _ButtonsRect;
        private FormDirtyTracking _DirtyTracking = FormDirtyTracking.Events;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the <see cref="Form"/> class.
        /// </summary>
        public Form()
        {
            _Focus.CanFocus = CanFocusField;
        }

        #endregion

        #region Public-Members

        /// <summary>
        /// Raised when <see cref="IsDirty"/> changes: on the first change after the form was clean, on
        /// <see cref="MarkClean"/>, and in <see cref="FormDirtyTracking.Snapshot"/> mode when a revert makes the form
        /// clean again. The argument is the new dirty state.
        /// </summary>
        public event Action<bool>? DirtyChanged;

        /// <summary>
        /// Raised by <see cref="Submit"/> when every visible field is valid.
        /// </summary>
        public event EventHandler? SubmitRequested;

        /// <summary>
        /// Raised by <see cref="Cancel"/>. The handler reads <see cref="IsDirty"/> and decides whether to confirm;
        /// the form never prompts.
        /// </summary>
        public event EventHandler? CancelRequested;

        /// <summary>
        /// Gets the number of fields, including hidden ones. Sections are not counted.
        /// </summary>
        public int FieldCount
        {
            get { return _Fields.Count; }
        }

        /// <summary>
        /// Gets the zero-based index of the focused field, or -1 when the form is empty or the buttons
        /// (<see cref="ShowButtons"/>) hold focus.
        /// </summary>
        public int FocusedIndex
        {
            get
            {
                int index = _Focus.FocusedIndex;
                return index >= 0 && index < _Fields.Count ? index : -1;
            }
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
        /// Gets or sets the style of section headings (<see cref="AddSection"/>). Defaults to bold palette color 6.
        /// </summary>
        public CellStyle SectionStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(6)).WithAttribute(CellAttributes.Bold, true);

        /// <summary>
        /// Gets or sets the style of field hints (<see cref="SetFieldHint"/>). Defaults to dim text.
        /// </summary>
        public CellStyle HintStyle { get; set; } = CellStyle.Default.WithAttribute(CellAttributes.Dim, true);

        /// <summary>
        /// Gets or sets the style of field errors drawn while <see cref="ShowFieldErrors"/> is on. Defaults to palette
        /// color 1 (red).
        /// </summary>
        public CellStyle ErrorStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(1));

        /// <summary>
        /// Gets or sets the text drawn before each field error, so an error is not conveyed by color alone.
        /// Defaults to <c>"! "</c>. Must not be null; may be empty.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string ErrorPrefix
        {
            get { return _ErrorPrefix; }
            set { _ErrorPrefix = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether stored field errors are drawn under their fields. Defaults to
        /// false: errors are still stored by <see cref="ValidateAll"/> and readable with <see cref="GetFieldError"/>,
        /// but the layout is the 1.7 layout.
        /// </summary>
        public bool ShowFieldErrors { get; set; }

        /// <summary>
        /// Gets or sets when the form validates fields on its own. Defaults to <see cref="FormValidationMode.OnSubmit"/>.
        /// </summary>
        public FormValidationMode ValidationMode { get; set; } = FormValidationMode.OnSubmit;

        /// <summary>
        /// Gets or sets the chord that calls <see cref="Submit"/>, or null for none (the default). Ctrl+S is the
        /// common choice. Checked after the focused field had the key.
        /// </summary>
        public KeyChord? SubmitKey { get; set; }

        /// <summary>
        /// Gets or sets the chord that calls <see cref="Cancel"/>, or null for none (the default). Esc is the common
        /// choice. Checked after the focused field had the key, so an open dropdown closes first.
        /// </summary>
        public KeyChord? CancelKey { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a <see cref="ButtonRow"/> with <see cref="SubmitLabel"/> and
        /// <see cref="CancelLabel"/> is drawn after the last field and joins the focus order. Defaults to false.
        /// </summary>
        public bool ShowButtons
        {
            get { return _ShowButtons; }
            set
            {
                _ShowButtons = value;
                if (value)
                    EnsureButtons();
            }
        }

        /// <summary>
        /// Gets the button row while <see cref="ShowButtons"/> has ever been on, for styling and extra buttons; null
        /// before that.
        /// </summary>
        public ButtonRow? Buttons
        {
            get { return _Buttons; }
        }

        /// <summary>
        /// Gets or sets the submit button's label. Defaults to <c>"Save"</c>. Must not be null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string SubmitLabel
        {
            get { return _SubmitLabel; }
            set
            {
                _SubmitLabel = value ?? throw new ArgumentNullException(nameof(value));
                if (_SubmitButton != null)
                    _SubmitButton.Label = value;
            }
        }

        /// <summary>
        /// Gets or sets the cancel button's label. Defaults to <c>"Cancel"</c>. Must not be null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string CancelLabel
        {
            get { return _CancelLabel; }
            set
            {
                _CancelLabel = value ?? throw new ArgumentNullException(nameof(value));
                if (_CancelButton != null)
                    _CancelButton.Label = value;
            }
        }

        /// <summary>
        /// Gets or sets where labels are drawn. Defaults to <see cref="FormLabelPlacement.Above"/>, the 1.7 layout.
        /// </summary>
        public FormLabelPlacement LabelPlacement { get; set; } = FormLabelPlacement.Above;

        /// <summary>
        /// Gets or sets the label column width with <see cref="FormLabelPlacement.Left"/>, including the two-column
        /// focus marker. Defaults to 0: the widest visible label plus three (the marker and a space), capped at a
        /// third of the width. Otherwise at least 1.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set below 0.</exception>
        public int LabelWidth
        {
            get { return _LabelWidth; }
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), "LabelWidth must be zero (automatic) or greater.");
                _LabelWidth = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether Up and Down that the focused field does not consume move focus to
        /// the previous or next field. Defaults to false. A <see cref="TextEditor"/> keeps its arrows; a
        /// <see cref="TextField"/> passes them on.
        /// </summary>
        public bool ArrowKeysMoveFocus { get; set; }

        /// <summary>
        /// Gets or sets how <see cref="IsDirty"/> is decided. Defaults to <see cref="FormDirtyTracking.Events"/>, the
        /// 1.7 behavior. Changing it re-baselines: the current values become the clean snapshot.
        /// </summary>
        public FormDirtyTracking DirtyTracking
        {
            get { return _DirtyTracking; }
            set
            {
                _DirtyTracking = value;
                MarkClean();
            }
        }

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
        /// Gets a value indicating whether the form has unsaved changes. With <see cref="FormDirtyTracking.Events"/>, a
        /// field widget reported a change since the form was built or last marked clean (only widgets implementing
        /// <see cref="IChangeNotifier"/> are tracked). With <see cref="FormDirtyTracking.Snapshot"/>, some field's
        /// snapshot differs from the one recorded by <see cref="MarkClean"/>; fields without a snapshot fall back to
        /// change events.
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

        /// <inheritdoc/>
        public int ContentHeight
        {
            get
            {
                if (_ContentHeight > 0)
                    return _ContentHeight;

                int total = _Sections.Count * 2;
                for (int i = 0; i < _Fields.Count; i++)
                {
                    if (_Fields[i].Visible)
                        total += (_Fields[i].Inline || LabelPlacement == FormLabelPlacement.Left ? 2 : 3) + (string.IsNullOrEmpty(_Fields[i].Hint) ? 0 : 1);
                }

                return total + (_ShowButtons ? 1 : 0);
            }
        }

        #endregion

        #region Public-Methods

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
            if (widget is IValueSnapshot snapshot)
                field.Snapshot = snapshot.GetSnapshot;
            field.CleanSnapshot = field.Snapshot?.Invoke();
            _Fields.Add(field);
            if (_Buttons != null)
                RebuildFocus();
            else
                _Focus.Register(widget);

            if (widget is IChangeNotifier notifier)
            {
                field.ChangedHandler = (s, e) => OnFieldChanged(field);
                notifier.Changed += field.ChangedHandler;
            }

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
        /// Adds a section heading before the next field added (or at the end when no field follows). A section
        /// takes no focus and does not shift field indexes; it is drawn in <see cref="SectionStyle"/> with one blank
        /// row before it, except at the top.
        /// </summary>
        /// <param name="title">The heading. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="title"/> is null.</exception>
        public void AddSection(string title)
        {
            if (title == null)
                throw new ArgumentNullException(nameof(title));

            _Sections.Add(new FormSection(_Fields.Count, title));
        }

        /// <summary>
        /// Sets or clears a one-line hint drawn under a field (wrapped to the field width), such as "Used as the
        /// branch prefix".
        /// </summary>
        /// <param name="index">The zero-based field index. Must be within [0, FieldCount).</param>
        /// <param name="hint">The hint, or null or empty for none.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is out of range.</exception>
        public void SetFieldHint(int index, string? hint)
        {
            CheckIndex(index);
            _Fields[index].Hint = string.IsNullOrEmpty(hint) ? null : hint;
        }

        /// <summary>
        /// Supplies a snapshot for a field whose widget does not implement <see cref="IValueSnapshot"/>, or replaces
        /// the widget's own, for <see cref="FormDirtyTracking.Snapshot"/> tracking. Null removes it (the field falls
        /// back to change events). The current value becomes the field's clean snapshot.
        /// </summary>
        /// <param name="index">The zero-based field index. Must be within [0, FieldCount).</param>
        /// <param name="snapshot">The snapshot function, or null.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is out of range.</exception>
        public void SetFieldSnapshot(int index, Func<string?>? snapshot)
        {
            CheckIndex(index);
            FormField field = _Fields[index];
            field.Snapshot = snapshot;
            field.CleanSnapshot = snapshot?.Invoke();
            RecomputeDirty();
        }

        /// <summary>
        /// Gets the stored error of a field (from <see cref="ValidateAll"/> or <see cref="ValidationMode"/>), or null.
        /// </summary>
        /// <param name="index">The zero-based field index. Must be within [0, FieldCount).</param>
        /// <returns>The error message, or null.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is out of range.</exception>
        public string? GetFieldError(int index)
        {
            CheckIndex(index);
            return _Fields[index].Error;
        }

        /// <summary>
        /// Clears every stored field error.
        /// </summary>
        public void ClearErrors()
        {
            foreach (FormField field in _Fields)
                field.Error = null;
        }

        /// <summary>
        /// Runs every visible field's validator, and for a <see cref="TextField"/> field its
        /// <see cref="TextField.Validator"/>, stores each error (clearing the errors of valid and hidden fields), and
        /// returns the errors in field order. <see cref="Validate"/> is unchanged and stores nothing.
        /// </summary>
        /// <returns>The errors; empty when the form is valid. Never null.</returns>
        public IReadOnlyList<FormFieldError> ValidateAll()
        {
            List<FormFieldError> errors = new List<FormFieldError>();
            for (int i = 0; i < _Fields.Count; i++)
            {
                FormField field = _Fields[i];
                if (!field.Visible)
                {
                    field.Error = null;
                    continue;
                }

                string? error = ValidateField(i);
                if (error != null)
                    errors.Add(new FormFieldError(i, field.Label, error));
            }

            return errors;
        }

        /// <summary>
        /// Validates with <see cref="ValidateAll"/>. When there are errors, focuses the first invalid field, raises
        /// nothing, and returns false; otherwise raises <see cref="SubmitRequested"/> and returns true.
        /// </summary>
        /// <returns><c>true</c> when the form was valid and submitted; otherwise <c>false</c>.</returns>
        public bool Submit()
        {
            IReadOnlyList<FormFieldError> errors = ValidateAll();
            if (errors.Count > 0)
            {
                FormFieldError first = errors[0];
                if (FocusScope.IsFocusable(_Fields[first.FieldIndex].Widget))
                    _Focus.SetFocus(first.FieldIndex);
                return false;
            }

            SubmitRequested?.Invoke(this, EventArgs.Empty);
            return true;
        }

        /// <summary>
        /// Raises <see cref="CancelRequested"/>.
        /// </summary>
        public void Cancel()
        {
            CancelRequested?.Invoke(this, EventArgs.Empty);
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
        /// Clears <see cref="IsDirty"/>, for example after saving. In <see cref="FormDirtyTracking.Snapshot"/> mode this
        /// also records each field's current snapshot as its clean value.
        /// </summary>
        public void MarkClean()
        {
            foreach (FormField field in _Fields)
            {
                field.EventDirty = false;
                field.CleanSnapshot = field.Snapshot?.Invoke();
            }

            SetDirty(false);
        }

        /// <summary>
        /// Removes every field and section and resets the focus ring, so the form can be rebuilt with a different
        /// field set at runtime (for example when a dependent selection changes which fields apply).
        /// The form becomes clean. The buttons stay when <see cref="ShowButtons"/> is on.
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < _Fields.Count; i++)
            {
                if (_Fields[i].Widget is IChangeNotifier notifier && _Fields[i].ChangedHandler != null)
                    notifier.Changed -= _Fields[i].ChangedHandler;
            }

            _Fields.Clear();
            _Sections.Clear();
            _Focus.Clear();
            if (_Buttons != null)
                _Focus.Register(_Buttons);
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
        public bool TryGetFocusRect(out Rect rect)
        {
            int index = _Focus.FocusedIndex;
            if (index >= _Fields.Count && _ShowButtons && _ButtonsRect.Height > 0)
            {
                rect = _ButtonsRect;
                return true;
            }

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
        /// visible fields are valid. Stores nothing and ignores <see cref="TextField.Validator"/>, as in 1.7; see
        /// <see cref="ValidateAll"/>.
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

        /// <summary>
        /// Routes a key: Tab and Shift+Tab move focus, other keys go to the focused field first; then
        /// <see cref="SubmitKey"/>, <see cref="CancelKey"/>, and with <see cref="ArrowKeysMoveFocus"/> Up and Down.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns><c>true</c> when the key was consumed; otherwise <c>false</c>.</returns>
        public bool HandleKey(KeyEvent key)
        {
            int before = _Focus.FocusedIndex;
            bool handled = _Focus.HandleKey(key);
            if (!handled)
            {
                KeyChord chord = KeyChord.FromKeyEvent(key);
                if (SubmitKey.HasValue && chord.Equals(SubmitKey.Value))
                {
                    Submit();
                    handled = true;
                }
                else if (CancelKey.HasValue && chord.Equals(CancelKey.Value))
                {
                    Cancel();
                    handled = true;
                }
                else if (ArrowKeysMoveFocus && key.Modifiers == KeyModifiers.None && (key.Code == KeyCode.Up || key.Code == KeyCode.Down))
                {
                    handled = _Focus.MoveFocus(key.Code == KeyCode.Down);
                }
            }

            AfterInput(before);
            return handled;
        }

        /// <summary>
        /// Gets the form's keys for the status bar: <see cref="SubmitKey"/> and <see cref="CancelKey"/> when set,
        /// and "Up/Down Field" while <see cref="ArrowKeysMoveFocus"/> is on. The focused field's own hints come
        /// first through the resolver. Part of <see cref="IKeyHintSource"/>.
        /// </summary>
        /// <returns>The hints, or null when none apply.</returns>
        public IReadOnlyList<KeyHint>? GetKeyHints()
        {
            List<KeyHint>? hints = null;
            if (SubmitKey.HasValue)
                (hints = hints ?? new List<KeyHint>()).Add(new KeyHint(SubmitKey.Value, _SubmitLabel, 8).WhileTyping());
            if (CancelKey.HasValue)
                (hints = hints ?? new List<KeyHint>()).Add(new KeyHint(CancelKey.Value, _CancelLabel, 7).WhileTyping());
            if (ArrowKeysMoveFocus)
                (hints = hints ?? new List<KeyHint>()).Add(new KeyHint("Up/Down", "Field").WhileTyping());
            return hints;
        }

        /// <summary>
        /// Offers a bracketed paste to the focused field when it is an <see cref="IPasteTarget"/>. Part of
        /// <see cref="IPasteTarget"/>.
        /// </summary>
        /// <param name="text">The pasted text. Must not be null.</param>
        /// <returns><c>true</c> when the focused field consumed the paste; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public bool HandlePaste(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            return FocusedLeaf is IPasteTarget target && !ReferenceEquals(target, this) && target.HandlePaste(text);
        }

        /// <inheritdoc/>
        public bool MoveFocus(bool forward)
        {
            int before = _Focus.FocusedIndex;
            bool moved;
            if (_Focus.Wrap)
            {
                // A wrapping ring never reports an edge, so nested use relies on WrapFocus being false.
                _Focus.MoveFocus(forward);
                moved = true;
            }
            else
            {
                moved = _Focus.MoveFocus(forward);
            }

            AfterInput(before);
            return moved;
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
        /// from <see cref="Theme.Disabled"/>, <see cref="SectionStyle"/> from <see cref="Theme.Accent"/> (bold),
        /// <see cref="HintStyle"/> from <see cref="Theme.Muted"/>, and <see cref="ErrorStyle"/> from
        /// <see cref="Theme.Error"/>, then forwards it to every field widget and the buttons.
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
            SectionStyle = theme.Accent.WithAttribute(CellAttributes.Bold, true);
            HintStyle = theme.Muted;
            ErrorStyle = theme.Error;
            for (int i = 0; i < _Fields.Count; i++)
                ThemeApplier.Apply(_Fields[i].Widget, theme);
            if (_Buttons != null)
                _Buttons.ApplyTheme(theme);
        }

        /// <summary>
        /// Routes a mouse event to the field under the pointer: a left press focuses that field, and the event
        /// is then forwarded (in field-widget-local coordinates) to the field's widget when it is itself
        /// mouse-aware, so a click lands the caret in the right text field, toggles the right checkbox, and so
        /// on. A click on the buttons goes to them. Coordinates are form-local. Returns false when no field is
        /// under the pointer. Disabled fields are not focused by a click.
        /// </summary>
        /// <param name="mouse">The mouse event in form-local coordinates. Must not be null.</param>
        /// <returns><c>true</c> when a field consumed the event; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mouse"/> is null.</exception>
        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            int before = _Focus.FocusedIndex;
            if (_ShowButtons && _Buttons != null && _ButtonsRect.Height > 0 && _ButtonsRect.Contains(new Point(mouse.X, mouse.Y)))
            {
                if (mouse.Kind == MouseEventKind.Press && mouse.Button == MouseButton.Left)
                    _Focus.SetFocus(_Fields.Count);
                _Buttons.HandleMouse(new MouseEvent(mouse.Kind, mouse.Button, mouse.X - _ButtonsRect.X, mouse.Y - _ButtonsRect.Y, mouse.Modifiers, mouse.ClickCount));
                AfterInput(before);
                return true;
            }

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

                AfterInput(before);
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
            _ButtonsRect = default;
            _ContentWidth = width;
            bool left = LabelPlacement == FormLabelPlacement.Left;
            int labelWidth = left ? ResolveLabelWidth(width) : 0;
            int y = 0;
            for (int i = 0; i < _Fields.Count && y < height; i++)
            {
                y = DrawSections(surface, i, y, width, height);
                FormField field = _Fields[i];
                if (!field.Visible || y >= height)
                    continue;

                bool focused = _Focus.FocusedIndex == i;
                bool enabled = FocusScope.IsFocusable(field.Widget);
                int blockTop = y;
                CellStyle labelStyle = focused ? FocusedLabelStyle : LabelStyle;
                if (!enabled)
                    labelStyle = DisabledLabelStyle.Over(labelStyle);

                int widgetX = 2;
                if (!field.Inline && !left)
                {
                    surface.DrawText(0, y, (focused ? "> " : "  ") + field.Label, labelStyle);
                    y++;
                }
                else if (!field.Inline)
                {
                    // Keep at least one blank column between a capped label and its field.
                    surface.DrawText(0, y, TextFit.Truncate((focused ? "> " : "  ") + field.Label, Math.Max(1, labelWidth - 1)), labelStyle);
                    widgetX = labelWidth;
                }

                int widgetWidth = Math.Max(0, width - widgetX);
                int fieldHeight = Math.Max(1, field.Widget.Measure(new Size(widgetWidth, Math.Max(0, height - y))).Height);
                Rect widgetRect = new Rect(widgetX, y, widgetWidth, Math.Max(0, Math.Min(fieldHeight, height - y)));
                if (field.Inline && focused)
                    surface.DrawText(0, y, "> ", FocusedLabelStyle);
                if (y < height)
                    field.Widget.Render(new SurfaceView(surface, widgetRect));

                y += fieldHeight;
                int noteX = field.Inline ? 2 : widgetX;
                if (ShowFieldErrors && field.Error != null)
                {
                    foreach (string line in TextWords.Wrap(_ErrorPrefix + field.Error, Math.Max(1, width - noteX)))
                    {
                        if (y >= height)
                            break;
                        surface.DrawText(noteX, y, line, ErrorStyle);
                        y++;
                    }
                }

                if (field.Hint != null)
                {
                    foreach (string line in TextWords.Wrap(field.Hint, Math.Max(1, width - noteX)))
                    {
                        if (y >= height)
                            break;
                        surface.DrawText(noteX, y, line, HintStyle);
                        y++;
                    }
                }

                y++;
                _FieldTops.Add(blockTop);
                _FieldHeights.Add(Math.Max(1, y - blockTop));
                _FieldWidgetRects.Add(widgetRect);
                _RenderedIndexes.Add(i);
            }

            y = DrawSections(surface, _Fields.Count, y, width, height);
            if (_ShowButtons && _Buttons != null && y < height)
            {
                int rows = Math.Max(1, _Buttons.LineCount(Math.Max(1, width - 2)));
                _ButtonsRect = new Rect(2, y, Math.Max(0, width - 2), Math.Min(rows, height - y));
                if (_Focus.FocusedIndex >= _Fields.Count)
                    surface.DrawText(0, y, "> ", FocusedLabelStyle);
                _Buttons.Render(new SurfaceView(surface, _ButtonsRect));
                y += rows;
            }

            _ContentHeight = y;
        }

        #endregion

        #region Private-Methods

        private int DrawSections(ISurface surface, int beforeField, int y, int width, int height)
        {
            foreach (FormSection section in _Sections)
            {
                if (section.BeforeField != beforeField || y >= height)
                    continue;

                if (y > 0)
                    y++;
                if (y >= height)
                    break;

                surface.DrawText(0, y, TextFit.Truncate(section.Title, width), SectionStyle);
                y++;
            }

            return y;
        }

        private int ResolveLabelWidth(int width)
        {
            if (_LabelWidth > 0)
                return Math.Min(_LabelWidth, Math.Max(1, width - 1));

            int widest = 0;
            foreach (FormField field in _Fields)
            {
                if (field.Visible && !field.Inline)
                    widest = Math.Max(widest, TextFit.Width(field.Label));
            }

            return Math.Max(1, Math.Min(widest + 3, Math.Max(3, width / 3)));
        }

        private string? ValidateField(int index)
        {
            FormField field = _Fields[index];
            string? error = field.Validator?.Invoke();
            if (string.IsNullOrEmpty(error) && field.Widget is TextField text && text.Validator != null && !text.Validate())
                error = text.Error;
            field.Error = string.IsNullOrEmpty(error) ? null : error;
            return field.Error;
        }

        private void AfterInput(int beforeIndex)
        {
            int after = _Focus.FocusedIndex;
            if (ValidationMode == FormValidationMode.OnLeave && beforeIndex != after && beforeIndex >= 0 && beforeIndex < _Fields.Count && _Fields[beforeIndex].Visible)
                ValidateField(beforeIndex);

            if (_DirtyTracking == FormDirtyTracking.Snapshot)
                RecomputeDirty();
        }

        private void EnsureButtons()
        {
            if (_Buttons != null)
                return;

            _Buttons = new ButtonRow();
            _SubmitButton = _Buttons.Add(_SubmitLabel, () => Submit());
            _CancelButton = _Buttons.Add(_CancelLabel, Cancel);
            RebuildFocus();
        }

        private void RebuildFocus()
        {
            IFocusable? focused = _Focus.Focused;
            _Focus.Clear();
            foreach (FormField field in _Fields)
                _Focus.Register(field.Focusable);
            if (_Buttons != null)
                _Focus.Register(_Buttons);

            int index = -1;
            for (int i = 0; i < _Fields.Count; i++)
            {
                if (ReferenceEquals(_Fields[i].Focusable, focused))
                    index = i;
            }

            if (index < 0 && _Buttons != null && ReferenceEquals(focused, _Buttons))
                index = _Fields.Count == 1 ? 0 : _Fields.Count;
            if (index >= 0)
                _Focus.SetFocus(index);
        }

        private bool CanFocusField(IFocusable widget)
        {
            if (_Buttons != null && ReferenceEquals(widget, _Buttons))
                return _ShowButtons;

            for (int i = 0; i < _Fields.Count; i++)
            {
                if (ReferenceEquals(_Fields[i].Focusable, widget))
                    return _Fields[i].Visible;
            }

            return true;
        }

        private void OnFieldChanged(FormField field)
        {
            if (!_TrackChanges)
                return;

            field.EventDirty = true;
            int index = _Fields.IndexOf(field);
            if (index >= 0 && field.Visible && (ValidationMode == FormValidationMode.OnChange || field.Error != null))
                ValidateField(index);

            if (_DirtyTracking == FormDirtyTracking.Snapshot)
                RecomputeDirty();
            else
                SetDirty(true);
        }

        private void RecomputeDirty()
        {
            if (!_TrackChanges || _DirtyTracking != FormDirtyTracking.Snapshot)
                return;

            bool dirty = _Fields.Any(f => f.Snapshot != null
                ? !string.Equals(f.Snapshot(), f.CleanSnapshot, StringComparison.Ordinal)
                : f.EventDirty);
            SetDirty(dirty);
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

        #endregion
    }
}

namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Theming;
    using TUIKit.Unicode;

    /// <summary>
    /// A multi-step flow for setup, import, onboarding, and "create from template" screens: a header ("Step 2 of 5:
    /// Repository"), a step list showing what is done and what is next, the current step's content, a validation
    /// error row, and Back, Skip, Next or Finish, and Cancel buttons. Steps validate before moving forward, can be
    /// optional, and can drop out of the flow based on an earlier answer (<see cref="WizardStep.IsApplicable"/>).
    /// Show it in a <see cref="Modals.WizardModal"/> to get a dialog whose completion reports the outcome.
    /// </summary>
    /// <remarks>
    /// Focus lands in a step's content when the step is entered; Tab moves through the content and then the
    /// buttons. <see cref="NextStepKey"/> and <see cref="PreviousStepKey"/> (Alt+Right and Alt+Left) and
    /// <see cref="NextStepAltKey"/> and <see cref="PreviousStepAltKey"/> (Alt+F and Alt+B, which macOS terminals send
    /// for Option+arrows) move between steps; the wizard checks them before the content. Enter goes to the focused
    /// widget or button and Esc is left to the host. As a new widget it follows the theme it is given. Not
    /// thread-safe: use it from the UI loop.
    /// </remarks>
    public sealed class Wizard : IWidget, IFocusContainer, IFocusAware, IMouseAware, IFocusPathNode, IKeyHintSource, IThemeable, IPasteTarget
    {
        #region Private-Members

        private const string _DefaultDoneGlyph = "✓";

        private readonly List<WizardStep> _Steps;
        private readonly HashSet<int> _Visited = new HashSet<int>();
        private readonly FocusManager _Focus = new FocusManager();
        private readonly ButtonRow _Buttons = new ButtonRow();
        private int _Current;
        private string? _Error;
        private bool _Focused;
        private bool _Ascii;
        private Rect _ContentRect;
        private Rect _ButtonsRect;
        private Rect _ListRect;
        private readonly List<int> _ListHitSteps = new List<int>();
        private readonly List<int> _ListHitStarts = new List<int>();
        private string _HeaderFormat = "Step {0} of {1}: {2}";
        private string _DoneGlyph = _DefaultDoneGlyph;
        private string _CurrentGlyph = ">";
        private string _PendingGlyph = " ";
        private string _BackLabel = "Back";
        private string _SkipLabel = "Skip";
        private string _NextLabel = "Next";
        private string _FinishLabel = "Finish";
        private string _CancelLabel = "Cancel";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the <see cref="Wizard"/> class on its first applicable step.
        /// </summary>
        /// <param name="steps">The steps in order. Must not be null and must hold at least one step and no null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="steps"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when there are no steps or a step is null.</exception>
        public Wizard(IEnumerable<WizardStep> steps)
        {
            if (steps == null)
                throw new ArgumentNullException(nameof(steps));

            _Steps = steps.ToList();
            if (_Steps.Count == 0)
                throw new ArgumentException("A wizard needs at least one step.", nameof(steps));
            if (_Steps.Any(s => s == null))
                throw new ArgumentException("Steps must not contain null.", nameof(steps));

            foreach (WizardStep step in _Steps)
            {
                if (step.Content is Form form)
                    form.WrapFocus = false;
            }

            _Focus.Wrap = true;
            _Buttons.Wrap = false;
            int first = NextApplicable(-1, 1);
            _Current = first >= 0 ? first : 0;
            _Visited.Add(_Current);
            EnterStep(true);
        }

        #endregion

        #region Public-Members

        /// <summary>
        /// Raised before every move between steps; set <see cref="WizardStepChangingEventArgs.Cancel"/> to veto it.
        /// </summary>
        public event EventHandler<WizardStepChangingEventArgs>? StepChanging;

        /// <summary>
        /// Raised after every move between steps.
        /// </summary>
        public event EventHandler<WizardStepChangedEventArgs>? StepChanged;

        /// <summary>
        /// Raised when <see cref="Next"/> (or <see cref="Skip"/>) leaves the last applicable step.
        /// </summary>
        public event EventHandler? Finished;

        /// <summary>
        /// Raised by <see cref="Cancel"/> and the Cancel button.
        /// </summary>
        public event EventHandler? Cancelled;

        /// <summary>
        /// Gets every step, applicable or not, in order. Never null.
        /// </summary>
        public IReadOnlyList<WizardStep> Steps
        {
            get { return _Steps; }
        }

        /// <summary>
        /// Gets the index of the current step in <see cref="Steps"/>.
        /// </summary>
        public int CurrentIndex
        {
            get { return _Current; }
        }

        /// <summary>
        /// Gets the current step. Never null.
        /// </summary>
        public WizardStep CurrentStep
        {
            get { return _Steps[_Current]; }
        }

        /// <summary>
        /// Gets a value indicating whether no applicable step comes before the current one.
        /// </summary>
        public bool IsFirst
        {
            get { return NextApplicable(_Current, -1) < 0; }
        }

        /// <summary>
        /// Gets a value indicating whether no applicable step comes after the current one, so Next reads Finish.
        /// </summary>
        public bool IsLast
        {
            get { return NextApplicable(_Current, 1) < 0; }
        }

        /// <summary>
        /// Gets the last validation message, or null. Cleared by every successful move.
        /// </summary>
        public string? Error
        {
            get { return _Error; }
        }

        /// <summary>
        /// Gets the wizard's buttons, for styling. The wizard rebuilds them on every step change.
        /// </summary>
        public ButtonRow Buttons
        {
            get { return _Buttons; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether <see cref="GoTo"/> may jump forward to a step not yet visited,
        /// without validating the steps in between. Defaults to false.
        /// </summary>
        public bool AllowJumpAhead { get; set; }

        /// <summary>
        /// Gets or sets where the step list is drawn. Defaults to <see cref="WizardStepListPlacement.Top"/>.
        /// </summary>
        public WizardStepListPlacement StepListPlacement { get; set; } = WizardStepListPlacement.Top;

        /// <summary>
        /// Gets or sets the header text: a composite format with {0} the step's position among the applicable
        /// steps, {1} their count, and {2} the title. Defaults to <c>"Step {0} of {1}: {2}"</c>; the localization hook.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        /// <exception cref="ArgumentException">Thrown when the format is not a valid composite format for three arguments.</exception>
        public string HeaderFormat
        {
            get { return _HeaderFormat; }
            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));

                try
                {
                    string.Format(CultureInfo.InvariantCulture, value, 1, 2, "x");
                }
                catch (FormatException ex)
                {
                    throw new ArgumentException("HeaderFormat is not a valid composite format: " + ex.Message, nameof(value), ex);
                }

                _HeaderFormat = value;
            }
        }

        /// <summary>
        /// Gets or sets the step-list glyph for a done step. Defaults to a check mark (U+2713), drawn as <c>"x"</c> on a
        /// theme with ASCII borders. Must not be null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string DoneGlyph
        {
            get { return _DoneGlyph; }
            set { _DoneGlyph = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>
        /// Gets or sets the step-list glyph for the current step. Defaults to <c>"&gt;"</c>. Must not be null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string CurrentGlyph
        {
            get { return _CurrentGlyph; }
            set { _CurrentGlyph = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>
        /// Gets or sets the step-list glyph for a pending step. Defaults to a space. Must not be null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string PendingGlyph
        {
            get { return _PendingGlyph; }
            set { _PendingGlyph = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>
        /// Gets or sets the Back button label. Defaults to <c>"Back"</c>. Must not be null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string BackLabel
        {
            get { return _BackLabel; }
            set { _BackLabel = value ?? throw new ArgumentNullException(nameof(value)); RebuildButtons(); }
        }

        /// <summary>
        /// Gets or sets the Skip button label (shown on optional steps). Defaults to <c>"Skip"</c>. Must not be null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string SkipLabel
        {
            get { return _SkipLabel; }
            set { _SkipLabel = value ?? throw new ArgumentNullException(nameof(value)); RebuildButtons(); }
        }

        /// <summary>
        /// Gets or sets the Next button label. Defaults to <c>"Next"</c>. Must not be null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string NextLabel
        {
            get { return _NextLabel; }
            set { _NextLabel = value ?? throw new ArgumentNullException(nameof(value)); RebuildButtons(); }
        }

        /// <summary>
        /// Gets or sets the label of the Next button on the last applicable step. Defaults to <c>"Finish"</c>. Must not be null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string FinishLabel
        {
            get { return _FinishLabel; }
            set { _FinishLabel = value ?? throw new ArgumentNullException(nameof(value)); RebuildButtons(); }
        }

        /// <summary>
        /// Gets or sets the Cancel button label. Defaults to <c>"Cancel"</c>. Must not be null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string CancelLabel
        {
            get { return _CancelLabel; }
            set { _CancelLabel = value ?? throw new ArgumentNullException(nameof(value)); RebuildButtons(); }
        }

        /// <summary>
        /// Gets or sets the chord that calls <see cref="Next"/>, or null for none. Defaults to Alt+Right.
        /// </summary>
        public KeyChord? NextStepKey { get; set; } = KeyChord.Parse("alt+right");

        /// <summary>
        /// Gets or sets the chord that calls <see cref="Back"/>, or null for none. Defaults to Alt+Left.
        /// </summary>
        public KeyChord? PreviousStepKey { get; set; } = KeyChord.Parse("alt+left");

        /// <summary>
        /// Gets or sets a second chord for <see cref="Next"/>, or null for none. Defaults to Alt+F, which macOS Terminal
        /// and iTerm2 (with "Natural text editing") send for Option+Right.
        /// </summary>
        public KeyChord? NextStepAltKey { get; set; } = KeyChord.Parse("alt+f");

        /// <summary>
        /// Gets or sets a second chord for <see cref="Back"/>, or null for none. Defaults to Alt+B, which macOS Terminal
        /// and iTerm2 (with "Natural text editing") send for Option+Left.
        /// </summary>
        public KeyChord? PreviousStepAltKey { get; set; } = KeyChord.Parse("alt+b");

        /// <summary>
        /// Gets or sets the header style. Defaults to bold palette color 6; set from <see cref="Theme.Accent"/> by a theme.
        /// </summary>
        public CellStyle HeaderStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(6)).WithAttribute(CellAttributes.Bold, true);

        /// <summary>
        /// Gets or sets the style of done steps in the list. Defaults to palette color 2; from <see cref="Theme.Success"/>.
        /// </summary>
        public CellStyle DoneStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(2));

        /// <summary>
        /// Gets or sets the style of the current step in the list. Defaults to bold; from <see cref="Theme.Text"/> (bold).
        /// </summary>
        public CellStyle CurrentStyle { get; set; } = CellStyle.Default.WithAttribute(CellAttributes.Bold, true);

        /// <summary>
        /// Gets or sets the style of pending steps in the list. Defaults to palette color 8; from <see cref="Theme.Muted"/>.
        /// </summary>
        public CellStyle PendingStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(8));

        /// <summary>
        /// Gets or sets the style of the error row. Defaults to palette color 1; from <see cref="Theme.Error"/>.
        /// </summary>
        public CellStyle ErrorStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(1));

        /// <inheritdoc/>
        public IFocusable? FocusedChild
        {
            get { return _Focus.Focused; }
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

        #endregion

        #region Public-Methods

        /// <summary>
        /// Validates the current step and moves to the next applicable one, or on the last step raises
        /// <see cref="Finished"/>. On a validation failure the message is stored in <see cref="Error"/>, shown, and
        /// the wizard stays.
        /// </summary>
        /// <returns><c>true</c> when it moved or finished; <c>false</c> on a validation failure or a veto.</returns>
        public bool Next()
        {
            string? error = CurrentStep.Validate?.Invoke();
            if (!string.IsNullOrEmpty(error))
            {
                _Error = error;
                return false;
            }

            int next = NextApplicable(_Current, 1);
            if (next < 0)
            {
                _Error = null;
                Finished?.Invoke(this, EventArgs.Empty);
                return true;
            }

            return MoveTo(next, true);
        }

        /// <summary>
        /// Moves to the previous applicable step without validating.
        /// </summary>
        /// <returns><c>true</c> when it moved; <c>false</c> on the first step or a veto.</returns>
        public bool Back()
        {
            int previous = NextApplicable(_Current, -1);
            return previous >= 0 && MoveTo(previous, false);
        }

        /// <summary>
        /// Moves forward without validating, only on an <see cref="WizardStep.Optional"/> step; on the last step this
        /// raises <see cref="Finished"/>.
        /// </summary>
        /// <returns><c>true</c> when it moved or finished; <c>false</c> on a required step or a veto.</returns>
        public bool Skip()
        {
            if (!CurrentStep.Optional)
                return false;

            int next = NextApplicable(_Current, 1);
            if (next < 0)
            {
                _Error = null;
                Finished?.Invoke(this, EventArgs.Empty);
                return true;
            }

            return MoveTo(next, true);
        }

        /// <summary>
        /// Moves to a step. Backward moves always apply. A forward move needs the target to have been visited, unless
        /// <see cref="AllowJumpAhead"/> is true, and validates every applicable step from the current one up to the
        /// target unless <see cref="AllowJumpAhead"/> is true; the first failure stops there with its error.
        /// </summary>
        /// <param name="index">The step index in <see cref="Steps"/>.</param>
        /// <returns><c>true</c> when the wizard is now on that step; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is out of range.</exception>
        public bool GoTo(int index)
        {
            if (index < 0 || index >= _Steps.Count)
                throw new ArgumentOutOfRangeException(nameof(index), "The step index must be within [0, Steps.Count).");

            if (index == _Current || !_Steps[index].Applies())
                return index == _Current;

            if (index < _Current)
                return MoveTo(index, false);

            if (!AllowJumpAhead)
            {
                if (!_Visited.Contains(index))
                    return false;

                for (int i = _Current; i < index; i++)
                {
                    if (!_Steps[i].Applies())
                        continue;

                    string? error = _Steps[i].Validate?.Invoke();
                    if (string.IsNullOrEmpty(error))
                        continue;

                    if (i != _Current)
                        MoveTo(i, true);
                    _Error = error;
                    return false;
                }
            }

            return MoveTo(index, true);
        }

        /// <summary>
        /// Raises <see cref="Cancelled"/>.
        /// </summary>
        public void Cancel()
        {
            Cancelled?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Routes a key: the step keys first, then Tab through the content and buttons, then the focused widget.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns><c>true</c> when the key was consumed; otherwise <c>false</c>.</returns>
        public bool HandleKey(KeyEvent key)
        {
            KeyChord chord = KeyChord.FromKeyEvent(key);
            if (Matches(chord, NextStepKey) || Matches(chord, NextStepAltKey))
            {
                Next();
                return true;
            }

            if (Matches(chord, PreviousStepKey) || Matches(chord, PreviousStepAltKey))
            {
                Back();
                return true;
            }

            return _Focus.HandleKey(key);
        }

        /// <inheritdoc/>
        public bool MoveFocus(bool forward)
        {
            return _Focus.MoveFocus(forward);
        }

        /// <inheritdoc/>
        public void FocusEdge(bool first)
        {
            _Focus.FocusEdge(first);
        }

        /// <summary>
        /// Forwards focus changes to the focused child. Part of <see cref="IFocusAware"/>.
        /// </summary>
        /// <param name="focused"><c>true</c> when the wizard gained focus; otherwise <c>false</c>.</param>
        public void OnFocusChanged(bool focused)
        {
            _Focused = focused;
            if (_Focus.Focused is IFocusAware aware)
                aware.OnFocusChanged(focused);
        }

        /// <summary>
        /// Offers a paste to the focused leaf when it is an <see cref="IPasteTarget"/>. Part of <see cref="IPasteTarget"/>.
        /// </summary>
        /// <param name="text">The pasted text. Must not be null.</param>
        /// <returns><c>true</c> when the leaf consumed it; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public bool HandlePaste(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            if (FocusedLeaf is IPasteTarget leaf && !ReferenceEquals(leaf, this) && leaf.HandlePaste(text))
                return true;
            return _Focus.Focused is IPasteTarget direct && !ReferenceEquals(direct, this) && !ReferenceEquals(direct, FocusedLeaf) && direct.HandlePaste(text);
        }

        /// <summary>
        /// Gets the wizard's keys: Next step and Previous step for the keys that are set, then the content's own hints
        /// arrive through the resolver. Part of <see cref="IKeyHintSource"/>.
        /// </summary>
        /// <returns>The hints, or null when no step key is set.</returns>
        public IReadOnlyList<KeyHint>? GetKeyHints()
        {
            List<KeyHint>? hints = null;
            if (NextStepKey.HasValue)
                (hints = hints ?? new List<KeyHint>()).Add(new KeyHint(NextStepKey.Value, IsLast ? _FinishLabel : "Next step", 6).WhileTyping());
            if (PreviousStepKey.HasValue && !IsFirst)
                (hints = hints ?? new List<KeyHint>()).Add(new KeyHint(PreviousStepKey.Value, "Previous step", 5).WhileTyping());
            return hints;
        }

        /// <summary>
        /// Applies a theme: the header from <see cref="Theme.Accent"/> (bold), done steps from <see cref="Theme.Success"/>,
        /// the current step from <see cref="Theme.Text"/> (bold), pending steps from <see cref="Theme.Muted"/>, and the
        /// error from <see cref="Theme.Error"/>; ASCII glyphs on a theme with ASCII borders; then the theme goes to the
        /// buttons and every step's content.
        /// </summary>
        /// <param name="theme">The theme. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
        public void ApplyTheme(Theme theme)
        {
            if (theme == null)
                throw new ArgumentNullException(nameof(theme));

            HeaderStyle = theme.Accent.WithAttribute(CellAttributes.Bold, true);
            DoneStyle = theme.Success;
            CurrentStyle = theme.Text.WithAttribute(CellAttributes.Bold, true);
            PendingStyle = theme.Muted;
            ErrorStyle = theme.Error;
            _Ascii = theme.UseAsciiBorders;
            _Buttons.ApplyTheme(theme);
            foreach (WizardStep step in _Steps)
                ThemeApplier.Apply(step.Content, theme);
        }

        /// <summary>
        /// Routes a mouse event: a click on a step in the list jumps to it (through <see cref="GoTo"/>), and events in
        /// the content or the buttons go to them in local coordinates. Coordinates are wizard-local.
        /// </summary>
        /// <param name="mouse">The mouse event. Must not be null.</param>
        /// <returns><c>true</c> when the event was consumed; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mouse"/> is null.</exception>
        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            Point point = new Point(mouse.X, mouse.Y);
            bool press = mouse.Kind == MouseEventKind.Press && mouse.Button == MouseButton.Left;
            if (_ButtonsRect.Contains(point))
            {
                if (press)
                    _Focus.SetFocus(_Focus.Count - 1);
                _Buttons.HandleMouse(new MouseEvent(mouse.Kind, mouse.Button, mouse.X - _ButtonsRect.X, mouse.Y - _ButtonsRect.Y, mouse.Modifiers, mouse.ClickCount));
                return true;
            }

            if (_ContentRect.Contains(point))
            {
                if (press && _Focus.Count > 1)
                    _Focus.SetFocus(0);
                if (CurrentStep.Content is IMouseAware aware)
                    aware.HandleMouse(new MouseEvent(mouse.Kind, mouse.Button, mouse.X - _ContentRect.X, mouse.Y - _ContentRect.Y, mouse.Modifiers, mouse.ClickCount));
                return true;
            }

            if (press && _ListRect.Contains(point))
            {
                for (int i = _ListHitSteps.Count - 1; i >= 0; i--)
                {
                    int offset = StepListPlacement == WizardStepListPlacement.Left ? mouse.Y - _ListRect.Y : mouse.X - _ListRect.X;
                    if (offset >= _ListHitStarts[i])
                    {
                        GoTo(_ListHitSteps[i]);
                        return true;
                    }
                }
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
            _ContentRect = default;
            _ButtonsRect = default;
            _ListRect = default;
            _ListHitSteps.Clear();
            _ListHitStarts.Clear();
            if (width <= 0 || height <= 0)
                return;

            List<int> applicable = ApplicableSteps();
            int position = applicable.IndexOf(_Current) + 1;
            string header = string.Format(CultureInfo.CurrentCulture, _HeaderFormat, position, applicable.Count, CurrentStep.Title);
            surface.DrawText(0, 0, TextFit.Ellipsize(header, width), HeaderStyle);

            int top = 1;
            int contentX = 0;
            if (StepListPlacement == WizardStepListPlacement.Top && height > 1)
            {
                _ListRect = new Rect(0, 1, width, 1);
                int x = 0;
                foreach (int index in applicable)
                {
                    string item = Glyph(index) + " " + _Steps[index].Title;
                    if (x >= width)
                        break;
                    _ListHitSteps.Add(index);
                    _ListHitStarts.Add(x);
                    x += surface.DrawText(x, 1, TextFit.Truncate(item, width - x), StyleOf(index));
                    x += 2;
                }

                top = 3;
            }
            else if (StepListPlacement == WizardStepListPlacement.Left)
            {
                int column = Math.Min(Math.Max(4, width / 3), applicable.Max(i => TextFit.Width(_Steps[i].Title)) + 3);
                _ListRect = new Rect(0, 1, column, Math.Max(0, height - 1));
                for (int row = 0; row < applicable.Count && 1 + row < height; row++)
                {
                    int index = applicable[row];
                    _ListHitSteps.Add(index);
                    _ListHitStarts.Add(row);
                    surface.DrawText(0, 1 + row, TextFit.Truncate(Glyph(index) + " " + _Steps[index].Title, column - 1), StyleOf(index));
                }

                contentX = column + 1;
                top = 2;
            }
            else
            {
                top = 2;
            }

            int buttonRows = Math.Max(1, _Buttons.LineCount(Math.Max(1, width - contentX)));
            int errorRows = _Error != null ? 1 : 0;
            int contentHeight = Math.Max(0, height - top - buttonRows - errorRows - 1);
            _ContentRect = new Rect(contentX, Math.Min(top, height), Math.Max(0, width - contentX), contentHeight);
            if (contentHeight > 0)
                CurrentStep.Content.Render(new SurfaceView(surface, _ContentRect));

            int y = top + contentHeight;
            if (_Error != null && y < height)
            {
                surface.DrawText(contentX, y, TextFit.Ellipsize("! " + _Error, Math.Max(1, width - contentX)), ErrorStyle);
                y++;
            }

            y++;
            if (y < height)
            {
                _ButtonsRect = new Rect(contentX, y, Math.Max(0, width - contentX), Math.Min(buttonRows, height - y));
                _Buttons.Render(new SurfaceView(surface, _ButtonsRect));
            }
        }

        #endregion

        #region Private-Methods

        private static bool Matches(KeyChord chord, KeyChord? target)
        {
            return target.HasValue && chord.Equals(target.Value);
        }

        private bool MoveTo(int index, bool forward)
        {
            int from = _Current;
            EventHandler<WizardStepChangingEventArgs>? changing = StepChanging;
            if (changing != null)
            {
                WizardStepChangingEventArgs args = new WizardStepChangingEventArgs(from, index);
                changing(this, args);
                if (args.Cancel)
                    return false;
            }

            if (_Focus.Focused is IFocusAware leaving)
                leaving.OnFocusChanged(false);
            _Current = index;
            _Visited.Add(index);
            _Error = null;
            EnterStep(forward);
            StepChanged?.Invoke(this, new WizardStepChangedEventArgs(from, index));
            return true;
        }

        private void EnterStep(bool forward)
        {
            RebuildButtons();
            _Focus.Clear();
            IWidget content = CurrentStep.Content;
            bool contentFocusable = content is IFocusable focusable && FocusScope.IsFocusable(focusable);
            if (contentFocusable)
                _Focus.Register((IFocusable)content);
            _Focus.Register(_Buttons);
            if (contentFocusable)
            {
                _Focus.SetFocus(0);
                if (content is IFocusContainer container)
                    container.FocusEdge(true);
            }
            else
            {
                _Buttons.FocusEdge(true);
                int next = _Buttons.Buttons.ToList().FindIndex(b => b.Label == (IsLast ? _FinishLabel : _NextLabel));
                for (int i = 0; i < next; i++)
                    _Buttons.MoveFocus(true);
            }

            if (_Focus.Focused is IFocusAware aware)
                aware.OnFocusChanged(_Focused);
        }

        private void RebuildButtons()
        {
            if (_Steps == null || _Steps.Count == 0)
                return;

            _Buttons.Clear();
            Button back = _Buttons.Add(_BackLabel, () => Back());
            back.IsEnabled = !IsFirst;
            if (CurrentStep.Optional)
                _Buttons.Add(_SkipLabel, () => Skip());
            _Buttons.Add(IsLast ? _FinishLabel : _NextLabel, () => Next());
            _Buttons.Add(_CancelLabel, Cancel);
        }

        private int NextApplicable(int from, int direction)
        {
            for (int i = from + direction; i >= 0 && i < _Steps.Count; i += direction)
            {
                if (_Steps[i].Applies())
                    return i;
            }

            return -1;
        }

        private List<int> ApplicableSteps()
        {
            List<int> result = new List<int>();
            for (int i = 0; i < _Steps.Count; i++)
            {
                if (_Steps[i].Applies() || i == _Current)
                    result.Add(i);
            }

            return result;
        }

        private string Glyph(int index)
        {
            if (index == _Current)
                return _CurrentGlyph;
            if (index < _Current)
                return _Ascii && _DoneGlyph == _DefaultDoneGlyph ? "x" : _DoneGlyph;
            return _PendingGlyph;
        }

        private CellStyle StyleOf(int index)
        {
            if (index == _Current)
                return CurrentStyle;
            return index < _Current ? DoneStyle : PendingStyle;
        }

        #endregion
    }
}

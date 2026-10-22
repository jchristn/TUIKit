namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Theming;

    /// <summary>
    /// A question above a horizontal Yes/No radio group. Arrows select, Enter submits, and Escape
    /// cancels. Completed prompts show their answer until Reset is called. Rendered in the host's
    /// surface; this widget does not manage terminal scrollback. Use only from the UI loop.
    /// </summary>
    public sealed class ConfirmPrompt : IWidget, IFocusable, IMouseAware, IEnableable, IThemeable, IKeyHintSource
    {
        private readonly RadioGroup _Choices = new RadioGroup(new[] { "Yes", "No" })
        {
            Orientation = RadioGroupOrientation.Horizontal,
            SelectedMarker = "●",
            UnselectedMarker = "○"
        };
        private string _Question;
        private bool _UseAsciiGlyphs;
        private int _RenderedWidth;
        private int _RenderedChoiceRow = -1;

        /// <summary>Raised once on submission or cancellation. Read Result and IsCompleted for the outcome.</summary>
        public event EventHandler? Completed;

        /// <summary>Gets or sets the question. Must not be null; newlines and wrapping are supported.</summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string Question
        {
            get { return _Question; }
            set { _Question = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>Gets or sets the selected answer. Defaults to false (No). Changing it does not submit.</summary>
        public bool SelectedValue
        {
            get { return _Choices.SelectedIndex == 0; }
            set { _Choices.SelectedIndex = value ? 0 : 1; }
        }

        /// <summary>Gets whether Enter or Escape completed this prompt. Defaults to false.</summary>
        public bool IsCompleted { get; private set; }

        /// <summary>Gets the submitted answer, or null before completion and after cancellation.</summary>
        public bool? Result { get; private set; }

        /// <summary>Gets or sets whether input is accepted. Defaults to true.</summary>
        public bool IsEnabled
        {
            get { return _Choices.IsEnabled; }
            set { _Choices.IsEnabled = value; }
        }

        /// <summary>Gets or sets ASCII markers instead of Unicode. Defaults to false; ApplyTheme follows the theme.</summary>
        public bool UseAsciiGlyphs
        {
            get { return _UseAsciiGlyphs; }
            set
            {
                _UseAsciiGlyphs = value;
                _Choices.SelectedMarker = value ? "(o)" : "●";
                _Choices.UnselectedMarker = value ? "( )" : "○";
            }
        }

        /// <summary>Gets or sets the base style. Defaults to the terminal default.</summary>
        public CellStyle NormalStyle { get; set; } = CellStyle.Default;

        /// <summary>Gets or sets the marker and selection style. Defaults to cyan.</summary>
        public CellStyle AccentStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(6));

        /// <summary>Gets or sets the disabled overlay. Defaults to dim text.</summary>
        public CellStyle DisabledStyle { get; set; } = CellStyle.Default.WithAttribute(CellAttributes.Dim, true);

        /// <summary>Creates a confirmation prompt.</summary>
        /// <param name="question">The question. Must not be null.</param>
        /// <param name="defaultValue">The initial selection. Defaults to false (No).</param>
        /// <exception cref="ArgumentNullException">Thrown when question is null.</exception>
        public ConfirmPrompt(string question, bool defaultValue = false)
        {
            _Question = question ?? throw new ArgumentNullException(nameof(question));
            SelectedValue = defaultValue;
        }

        /// <summary>Starts a fresh interaction, clearing completion and cancellation.</summary>
        /// <param name="defaultValue">The new selection. Defaults to false (No).</param>
        public void Reset(bool defaultValue = false)
        {
            IsCompleted = false;
            Result = null;
            SelectedValue = defaultValue;
            _RenderedChoiceRow = -1;
        }

        /// <summary>Applies text, accent, disabled styles and the theme's ASCII preference.</summary>
        /// <param name="theme">The theme. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when theme is null.</exception>
        public void ApplyTheme(Theme theme)
        {
            if (theme == null)
                throw new ArgumentNullException(nameof(theme));
            NormalStyle = theme.Text;
            AccentStyle = theme.Accent;
            DisabledStyle = theme.Disabled;
            UseAsciiGlyphs = theme.UseAsciiBorders;
        }

        /// <summary>Handles unmodified Left/Right, Home/End, Enter to submit, and Escape to cancel.</summary>
        /// <param name="key">The decoded key.</param>
        /// <returns>Whether the key was consumed. False while disabled or completed.</returns>
        public bool HandleKey(KeyEvent key)
        {
            if (!IsEnabled || IsCompleted || key.Modifiers != KeyModifiers.None)
                return false;
            if (key.Code == KeyCode.Enter || key.Code == KeyCode.Escape)
            {
                Result = key.Code == KeyCode.Enter ? SelectedValue : (bool?)null;
                IsCompleted = true;
                Completed?.Invoke(this, EventArgs.Empty);
                return true;
            }
            return _Choices.HandleKey(key);
        }

        /// <summary>Selects an answer in the last rendered choice row. Clicking never submits.</summary>
        /// <param name="mouse">Widget-local mouse coordinates. Must not be null.</param>
        /// <returns>Whether the event was consumed. False outside the visible choices.</returns>
        /// <exception cref="ArgumentNullException">Thrown when mouse is null.</exception>
        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));
            if (!IsEnabled || IsCompleted || _RenderedChoiceRow < 0 || mouse.Y != _RenderedChoiceRow
                || mouse.X < 3 || mouse.X >= _RenderedWidth)
                return false;
            return _Choices.HandleMouse(new MouseEvent(mouse.Kind, mouse.Button, mouse.X - 3, 0, mouse.Modifiers, mouse.ClickCount));
        }

        /// <inheritdoc/>
        public IReadOnlyList<KeyHint>? GetKeyHints()
        {
            if (!IsEnabled || IsCompleted)
                return Array.Empty<KeyHint>();
            return new[] { new KeyHint("←/→", "Choose"), new KeyHint("Enter", "Submit"), new KeyHint("Esc", "Cancel") };
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            if (available.IsEmpty)
                return Size.Empty;
            int rows = PromptTextLayout.Wrap(Question, Math.Max(1, available.Width - 3)).Count;
            return new Size(available.Width, Math.Min(available.Height, rows + 1));
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));
            _RenderedChoiceRow = -1;
            _RenderedWidth = surface.Size.Width;
            if (surface.Size.IsEmpty)
                return;
            CellStyle normal = IsEnabled ? NormalStyle : DisabledStyle.Over(NormalStyle);
            CellStyle accent = IsEnabled ? AccentStyle.Over(NormalStyle) : DisabledStyle.Over(AccentStyle.Over(NormalStyle));
            surface.Fill(new Rect(0, 0, surface.Size.Width, surface.Size.Height), Cell.Blank(normal));
            IReadOnlyList<string> rows = PromptTextLayout.Wrap(Question, Math.Max(1, surface.Size.Width - 3));
            string connector = UseAsciiGlyphs ? "|" : "│";
            string marker = UseAsciiGlyphs ? (IsCompleted ? "+" : "*") : (IsCompleted ? "◇" : "◆");
            for (int y = 0; y < rows.Count && y < surface.Size.Height; y++)
            {
                surface.DrawText(0, y, y == 0 ? marker : connector, accent);
                surface.DrawText(3, y, rows[y], normal);
            }
            if (rows.Count >= surface.Size.Height)
                return;
            surface.DrawText(0, rows.Count, connector, accent);
            if (IsCompleted)
                surface.DrawText(3, rows.Count, Result.HasValue ? (Result.Value ? "Yes" : "No") : "Cancelled", normal);
            else if (surface.Size.Width > 3)
            {
                _RenderedChoiceRow = rows.Count;
                _Choices.NormalStyle = NormalStyle;
                _Choices.SelectedStyle = AccentStyle;
                _Choices.DisabledStyle = DisabledStyle;
                _Choices.Render(new SurfaceView(surface, new Rect(3, rows.Count, surface.Size.Width - 3, 1)));
            }
        }
    }
}

namespace TUIKit.Modals
{
    using System;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    /// <summary>
    /// A dialog that hosts a <see cref="Wizard"/>. It closes when the wizard finishes or is cancelled, so
    /// <see cref="Modal.Completion"/> resolves to a <see cref="WizardOutcome"/>. Esc calls
    /// <see cref="Widgets.Wizard.Cancel"/>; pastes go to the wizard's focused field.
    /// </summary>
    /// <remarks>
    /// The content area defaults to 64 by 16 cells, clamped to the screen; change it with
    /// <see cref="DialogModal.MinContentWidth"/>, <see cref="DialogModal.MaxContentWidth"/>, and the height
    /// properties. Not thread-safe: show and drive it from the UI loop.
    /// </remarks>
    public sealed class WizardModal : DialogModal, IThemeable
    {
        #region Private-Members

        private readonly Wizard _Wizard;
        private int _NaturalWidth = 64;
        private int _NaturalHeight = 16;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the <see cref="WizardModal"/> class.
        /// </summary>
        /// <param name="title">The dialog title. Must not be null.</param>
        /// <param name="wizard">The wizard. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public WizardModal(string title, Wizard wizard)
        {
            Title = title ?? throw new ArgumentNullException(nameof(title));
            _Wizard = wizard ?? throw new ArgumentNullException(nameof(wizard));
            _Wizard.Finished += (s, e) => RequestClose(WizardOutcome.Finished);
            _Wizard.Cancelled += (s, e) => RequestClose(WizardOutcome.Cancelled);
            _Wizard.OnFocusChanged(true);
        }

        #endregion

        #region Public-Members

        /// <summary>
        /// Gets the hosted wizard. Never null.
        /// </summary>
        public Wizard Wizard
        {
            get { return _Wizard; }
        }

        /// <summary>
        /// Gets or sets the natural content width in cells. Defaults to 64; at least 10.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set below 10.</exception>
        public int NaturalWidth
        {
            get { return _NaturalWidth; }
            set
            {
                if (value < 10)
                    throw new ArgumentOutOfRangeException(nameof(value), "The width must be at least 10.");
                _NaturalWidth = value;
            }
        }

        /// <summary>
        /// Gets or sets the natural content height in cells. Defaults to 16; at least 6.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set below 6.</exception>
        public int NaturalHeight
        {
            get { return _NaturalHeight; }
            set
            {
                if (value < 6)
                    throw new ArgumentOutOfRangeException(nameof(value), "The height must be at least 6.");
                _NaturalHeight = value;
            }
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Routes a key: Esc cancels the wizard; everything else goes to it. Always consumed, so keys never leak
        /// past the dialog.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns>Always <c>true</c>.</returns>
        public override bool HandleKey(KeyEvent key)
        {
            if (key.Code == KeyCode.Escape && key.Modifiers == KeyModifiers.None)
            {
                _Wizard.Cancel();
                return true;
            }

            _Wizard.HandleKey(key);
            return true;
        }

        /// <summary>
        /// Routes a mouse event inside the content area to the wizard. Coordinates are absolute screen cells.
        /// </summary>
        /// <param name="mouse">The mouse event. Must not be null.</param>
        /// <returns><c>true</c> when the wizard consumed it; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mouse"/> is null.</exception>
        public override bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            Rect content = ContentBounds;
            if (!content.Contains(new Point(mouse.X, mouse.Y)))
                return false;

            return _Wizard.HandleMouse(new MouseEvent(mouse.Kind, mouse.Button, mouse.X - content.X, mouse.Y - content.Y, mouse.Modifiers, mouse.ClickCount));
        }

        /// <summary>
        /// Applies a theme to the dialog border and the wizard. Part of <see cref="IThemeable"/>.
        /// </summary>
        /// <param name="theme">The theme. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
        public void ApplyTheme(Theme theme)
        {
            if (theme == null)
                throw new ArgumentNullException(nameof(theme));

            ApplyBorderTheme(theme);
            _Wizard.ApplyTheme(theme);
        }

        /// <inheritdoc/>
        protected override IPasteTarget? PasteTarget
        {
            get { return _Wizard; }
        }

        /// <inheritdoc/>
        protected override int MeasureContentWidth(int availableWidth)
        {
            return Math.Min(_NaturalWidth, Math.Max(1, availableWidth));
        }

        /// <inheritdoc/>
        protected override int MeasureContentHeight(int contentWidth)
        {
            return _NaturalHeight;
        }

        /// <inheritdoc/>
        protected override void RenderContent(ISurface content)
        {
            _Wizard.Render(content);
        }

        #endregion
    }
}

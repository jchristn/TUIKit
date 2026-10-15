namespace Test.Shared
{
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Modals;
    using TUIKit.Widgets;

    /// <summary>
    /// A <see cref="DialogModal"/> that hosts one <see cref="TextField"/> and names it as its paste target,
    /// used to test <see cref="DialogModal.HandlePaste"/>.
    /// </summary>
    public sealed class PasteDialogModal : DialogModal
    {
        private readonly TextField _Field = new TextField();
        private readonly bool _ExposeTarget;

        /// <summary>
        /// Initializes a new instance of the <see cref="PasteDialogModal"/> class.
        /// </summary>
        /// <param name="exposeTarget">True to return the field from the paste target; false to keep the default (none).</param>
        public PasteDialogModal(bool exposeTarget)
        {
            _ExposeTarget = exposeTarget;
        }

        /// <summary>
        /// Gets the hosted field.
        /// </summary>
        public TextField Field
        {
            get { return _Field; }
        }

        /// <inheritdoc/>
        protected override IPasteTarget? PasteTarget
        {
            get { return _ExposeTarget ? _Field : base.PasteTarget; }
        }

        /// <inheritdoc/>
        public override bool HandleKey(KeyEvent key)
        {
            return HandleDismiss(key, null) || _Field.HandleKey(key);
        }

        /// <inheritdoc/>
        protected override int MeasureContentWidth(int availableWidth)
        {
            return 20;
        }

        /// <inheritdoc/>
        protected override int MeasureContentHeight(int contentWidth)
        {
            return 1;
        }

        /// <inheritdoc/>
        protected override void RenderContent(ISurface content)
        {
            _Field.Render(content);
        }
    }
}

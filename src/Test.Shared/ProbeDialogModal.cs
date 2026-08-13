namespace Test.Shared
{
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Modals;

    /// <summary>
    /// A minimal concrete <see cref="DialogModal"/> used to exercise the base class in tests. It
    /// reports a fixed natural content size and fills its content area with a marker glyph so tests can
    /// find the resolved content rectangle.
    /// </summary>
    public sealed class ProbeDialogModal : DialogModal
    {
        private readonly int _NaturalWidth;
        private readonly int _NaturalHeight;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProbeDialogModal"/> class.
        /// </summary>
        /// <param name="naturalWidth">The natural content width to report.</param>
        /// <param name="naturalHeight">The natural content height to report.</param>
        public ProbeDialogModal(int naturalWidth, int naturalHeight)
        {
            _NaturalWidth = naturalWidth;
            _NaturalHeight = naturalHeight;
        }

        /// <summary>
        /// Gets the width of the content surface handed to <see cref="RenderContent"/> on the last render.
        /// </summary>
        public int LastContentWidth { get; private set; }

        /// <summary>
        /// Gets the height of the content surface handed to <see cref="RenderContent"/> on the last render.
        /// </summary>
        public int LastContentHeight { get; private set; }

        /// <summary>
        /// Exposes the protected truncation helper for testing.
        /// </summary>
        /// <param name="text">The text to fit.</param>
        /// <param name="maxWidth">The maximum width.</param>
        /// <returns>The fitted text.</returns>
        public string TruncatePublic(string? text, int maxWidth)
        {
            return Truncate(text, maxWidth);
        }

        /// <inheritdoc/>
        public override bool HandleKey(KeyEvent key)
        {
            return HandleDismiss(key, -1);
        }

        /// <inheritdoc/>
        protected override int MeasureContentWidth(int availableWidth)
        {
            return _NaturalWidth;
        }

        /// <inheritdoc/>
        protected override int MeasureContentHeight(int contentWidth)
        {
            return _NaturalHeight;
        }

        /// <inheritdoc/>
        protected override void RenderContent(ISurface content)
        {
            LastContentWidth = content.Size.Width;
            LastContentHeight = content.Size.Height;
            content.Fill(new Rect(0, 0, content.Size.Width, content.Size.Height), Cell.Glyph("#", CellStyle.Default, 1));
        }
    }
}

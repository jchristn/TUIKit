namespace TUIKit.Modals
{
    using System;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Layout;

    /// <summary>
    /// Base class for a centered, bordered dialog. It owns the work every boxed modal repeats: it
    /// measures the content, clamps the box to configurable minimum and maximum content sizes and to
    /// the screen, centers it, paints the background, draws the border and an optional title, draws an
    /// optional dim footer hint on the bottom edge, and hands the subclass an inner
    /// <see cref="ISurface"/> in local (zero-based) coordinates through <see cref="RenderContent"/>.
    /// Subclasses report their natural content size and draw their content; they never compute box
    /// geometry themselves.
    /// </summary>
    /// <remarks>
    /// Sizes are expressed in <em>content</em> cells (inside the border and <see cref="Modal.ContentPadding"/>).
    /// The border adds one cell on every side and the padding adds its own inset. Not thread-safe;
    /// render and key handling run on the host loop.
    /// </remarks>
    public abstract class DialogModal : Modal
    {
        private string? _Title;
        private string? _FooterHint;
        private int _MinContentWidth = 1;
        private int _MaxContentWidth = int.MaxValue;
        private int _MinContentHeight = 1;
        private int _MaxContentHeight = int.MaxValue;

        /// <summary>
        /// Gets or sets the title drawn centered on the top border, or null for no title. Defaults to null.
        /// </summary>
        public string? Title
        {
            get { return _Title; }
            set { _Title = value; }
        }

        /// <summary>
        /// Gets or sets a dim hint drawn centered on the bottom border (for example
        /// "Enter: ok · Esc: cancel"), or null for none. Defaults to null.
        /// </summary>
        public string? FooterHint
        {
            get { return _FooterHint; }
            set { _FooterHint = value; }
        }

        /// <summary>
        /// Gets or sets the minimum content width in cells. Defaults to 1.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set below 1 or above <see cref="MaxContentWidth"/>.</exception>
        public int MinContentWidth
        {
            get { return _MinContentWidth; }
            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Minimum content width must be at least 1.");
                if (value > _MaxContentWidth)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Minimum content width must not exceed the maximum.");
                _MinContentWidth = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum content width in cells. Defaults to <see cref="int.MaxValue"/>.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set below <see cref="MinContentWidth"/>.</exception>
        public int MaxContentWidth
        {
            get { return _MaxContentWidth; }
            set
            {
                if (value < _MinContentWidth)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Maximum content width must not be below the minimum.");
                _MaxContentWidth = value;
            }
        }

        /// <summary>
        /// Gets or sets the minimum content height in cells. Defaults to 1.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set below 1 or above <see cref="MaxContentHeight"/>.</exception>
        public int MinContentHeight
        {
            get { return _MinContentHeight; }
            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Minimum content height must be at least 1.");
                if (value > _MaxContentHeight)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Minimum content height must not exceed the maximum.");
                _MinContentHeight = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum content height in cells. Defaults to <see cref="int.MaxValue"/>.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set below <see cref="MinContentHeight"/>.</exception>
        public int MaxContentHeight
        {
            get { return _MaxContentHeight; }
            set
            {
                if (value < _MinContentHeight)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Maximum content height must not be below the minimum.");
                _MaxContentHeight = value;
            }
        }

        /// <summary>
        /// Gets or sets the border color style. Defaults to cyan (palette 6).
        /// </summary>
        public CellStyle BorderStyleColor { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(6));

        /// <summary>
        /// Gets or sets the style used for the background fill and the footer hint. The footer is drawn
        /// dim. Defaults to <see cref="CellStyle.Default"/>.
        /// </summary>
        public CellStyle BackgroundStyle { get; set; } = CellStyle.Default;

        /// <summary>
        /// Reports the natural content width the subclass would like, in cells, given the width the
        /// screen can offer. The base class clamps the result to <see cref="MinContentWidth"/>,
        /// <see cref="MaxContentWidth"/>, and the screen.
        /// </summary>
        /// <param name="availableWidth">The maximum content width the screen can accommodate.</param>
        /// <returns>The desired content width.</returns>
        protected abstract int MeasureContentWidth(int availableWidth);

        /// <summary>
        /// Reports the natural content height the subclass needs at the chosen content width. The base
        /// class clamps the result to <see cref="MinContentHeight"/>, <see cref="MaxContentHeight"/>,
        /// and the screen.
        /// </summary>
        /// <param name="contentWidth">The resolved content width in cells.</param>
        /// <returns>The desired content height.</returns>
        protected abstract int MeasureContentHeight(int contentWidth);

        /// <summary>
        /// Draws the dialog content into an inner surface whose origin is the content's top-left corner
        /// and whose size is the resolved content rectangle. All coordinates are local and clipped.
        /// </summary>
        /// <param name="content">The inner content surface. Never null.</param>
        protected abstract void RenderContent(ISurface content);

        /// <summary>
        /// Closes the dialog with the supplied result when the key is Escape and <see cref="Modal.CanClose"/>
        /// is true. A convenience for subclasses that cancel on Escape.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <param name="cancelResult">The result to complete with on Escape.</param>
        /// <returns><c>true</c> when the key was an accepted Escape and the dialog closed; otherwise <c>false</c>.</returns>
        protected bool HandleDismiss(KeyEvent key, object? cancelResult)
        {
            if (key.Code == KeyCode.Escape)
                return RequestClose(cancelResult);

            return false;
        }

        /// <summary>
        /// Truncates text to a maximum column width, appending an ellipsis when it is clipped.
        /// </summary>
        /// <param name="text">The text to fit. Null is treated as empty.</param>
        /// <param name="maxWidth">The maximum width in columns. Values below 1 yield an empty string.</param>
        /// <returns>The fitted text. Never null.</returns>
        protected static string Truncate(string? text, int maxWidth)
        {
            if (maxWidth < 1)
                return string.Empty;
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            if (TUIKit.Unicode.Graphemes.MeasureWidth(text!) <= maxWidth)
                return text!;

            if (maxWidth == 1)
                return "…";

            int take = maxWidth - 1;
            string clipped = text!.Length > take ? text.Substring(0, take) : text;
            while (clipped.Length > 0 && TUIKit.Unicode.Graphemes.MeasureWidth(clipped) > maxWidth - 1)
                clipped = clipped.Substring(0, clipped.Length - 1);

            return clipped + "…";
        }

        /// <inheritdoc/>
        public override void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            int screenWidth = surface.Size.Width;
            int screenHeight = surface.Size.Height;
            Padding pad = ContentPadding;

            int chromeWidth = 2 + pad.Horizontal;
            int chromeHeight = 2 + pad.Vertical;

            int availableContentWidth = Math.Max(1, screenWidth - chromeWidth);
            int contentWidth = Clamp(MeasureContentWidth(availableContentWidth), _MinContentWidth, _MaxContentWidth);
            contentWidth = Math.Min(contentWidth, availableContentWidth);

            int availableContentHeight = Math.Max(1, screenHeight - chromeHeight);
            int contentHeight = Clamp(MeasureContentHeight(contentWidth), _MinContentHeight, _MaxContentHeight);
            contentHeight = Math.Min(contentHeight, availableContentHeight);

            int boxWidth = contentWidth + chromeWidth;
            int boxHeight = contentHeight + chromeHeight;
            int boxX = Math.Max(0, (screenWidth - boxWidth) / 2);
            int boxY = Math.Max(0, (screenHeight - boxHeight) / 2);
            Rect box = new Rect(boxX, boxY, boxWidth, boxHeight);

            surface.Fill(box, Cell.Blank(BackgroundStyle));
            bool ascii = false;
            surface.DrawBox(box, BorderStyleColor, ascii ? BorderStyle.Ascii : BorderStyle.Rounded, _Title);
            DrawFooter(surface, box);

            Rect contentRect = new Rect(boxX + 1 + pad.Left, boxY + 1 + pad.Top, contentWidth, contentHeight);
            SurfaceView content = new SurfaceView(surface, contentRect);
            RenderContent(content);
        }

        private void DrawFooter(ISurface surface, Rect box)
        {
            if (string.IsNullOrEmpty(_FooterHint) || box.Width < 4)
                return;

            int available = box.Width - 4;
            string hint = Truncate(_FooterHint, available);
            if (hint.Length == 0)
                return;

            CellStyle style = BorderStyleColor.WithAttribute(CellAttributes.Dim, true);
            int width = TUIKit.Unicode.Graphemes.MeasureWidth(hint);
            int start = box.Left + 1 + Math.Max(0, ((box.Width - 2) - width) / 2);
            surface.DrawText(start, box.Bottom - 1, hint, style);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }
    }
}

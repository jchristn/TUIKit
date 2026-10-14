namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Input;

    /// <summary>
    /// A widget that shows Markdown laid out at its own width: tables sized from their content, wrapped lists
    /// and quotes that keep their indent, and highlighted code. Use it for Markdown that is not part of a
    /// scrolling log, such as a help page, a details panel, or a modal body; for a transcript use
    /// <see cref="Pane.WriteMarkdown(string, MarkdownRenderOptions)"/>. The view scrolls with
    /// <see cref="ScrollOffset"/> or the mouse wheel.
    /// </summary>
    /// <remarks>
    /// Layout is cached per width and recomputed when the width, <see cref="Markdown"/>, or
    /// <see cref="Options"/> changes. The options are copied when set; after editing a copy obtained from
    /// <see cref="Options"/> in place, call <see cref="Invalidate"/>. Not thread-safe: use it from the UI thread.
    /// </remarks>
    public sealed class MarkdownView : IWidget, IScrollExtent, IMouseAware
    {
        private string _Markdown;
        private MarkdownRenderOptions _Options;
        private int _ScrollOffset;
        private int _WheelStep = 3;
        private int _CachedWidth = -1;
        private IReadOnlyList<StyledText>? _CachedRows;

        /// <summary>
        /// Initializes an empty view with default <see cref="MarkdownRenderOptions"/>.
        /// </summary>
        public MarkdownView()
            : this(string.Empty, new MarkdownRenderOptions())
        {
        }

        /// <summary>
        /// Initializes a view showing the given Markdown with default <see cref="MarkdownRenderOptions"/>.
        /// </summary>
        /// <param name="markdown">The Markdown source. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="markdown"/> is null.</exception>
        public MarkdownView(string markdown)
            : this(markdown, new MarkdownRenderOptions())
        {
        }

        /// <summary>
        /// Initializes a view showing the given Markdown with the given options, which are copied.
        /// </summary>
        /// <param name="markdown">The Markdown source. Must not be null.</param>
        /// <param name="options">How the Markdown should look. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public MarkdownView(string markdown, MarkdownRenderOptions options)
        {
            _Markdown = markdown ?? throw new ArgumentNullException(nameof(markdown));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            _Options = options.Clone();
        }

        /// <summary>
        /// Gets or sets the Markdown source. Defaults to empty. Setting it keeps <see cref="ScrollOffset"/>, which
        /// is clamped at the next render. Must not be null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string Markdown
        {
            get { return _Markdown; }
            set
            {
                _Markdown = value ?? throw new ArgumentNullException(nameof(value));
                Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets how the Markdown should look. The value is copied on set, so later changes to the
        /// instance you passed have no effect. Getting returns the view's own copy; after changing it in place,
        /// call <see cref="Invalidate"/>. Must not be null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public MarkdownRenderOptions Options
        {
            get { return _Options; }
            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));

                _Options = value.Clone();
                Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the style used to clear the view before drawing. Defaults to <see cref="CellStyle.Default"/>.
        /// </summary>
        public CellStyle Background { get; set; } = CellStyle.Default;

        /// <summary>
        /// Gets or sets the first rendered row shown at the top of the view. Defaults to 0. Must be 0 or greater;
        /// values past the end are clamped at render so the last rows stay visible.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a negative value.</exception>
        public int ScrollOffset
        {
            get { return _ScrollOffset; }
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "ScrollOffset must be 0 or greater.");

                _ScrollOffset = value;
            }
        }

        /// <summary>
        /// Gets or sets the number of rows one mouse wheel notch scrolls. Defaults to 3. Minimum 1, maximum 100.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set outside 1 to 100.</exception>
        public int WheelStep
        {
            get { return _WheelStep; }
            set
            {
                if (value < 1 || value > 100)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "WheelStep must be between 1 and 100.");

                _WheelStep = value;
            }
        }

        /// <summary>
        /// Gets the number of rows the Markdown occupies at the width of the last render or measure, or 0 before
        /// either.
        /// </summary>
        public int ContentHeight
        {
            get { return _CachedRows == null ? 0 : _CachedRows.Count; }
        }

        /// <summary>
        /// Discards the cached layout so the next render lays the Markdown out again. Call it after changing the
        /// <see cref="Options"/> instance in place.
        /// </summary>
        public void Invalidate()
        {
            _CachedRows = null;
            _CachedWidth = -1;
        }

        /// <summary>
        /// Returns the rendered rows at a width, from the cache when the width has not changed.
        /// </summary>
        /// <param name="width">The width in columns. Must be 1 or greater.</param>
        /// <returns>The rendered rows. Never null.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="width"/> is less than 1.</exception>
        public IReadOnlyList<StyledText> RowsAt(int width)
        {
            if (width < 1)
                throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be 1 or greater.");

            if (_CachedRows == null || _CachedWidth != width)
            {
                _CachedRows = MarkdownRenderer.RenderCore(_Markdown, width, _Options, CancellationToken.None);
                _CachedWidth = width;
            }

            return _CachedRows;
        }

        /// <summary>
        /// Always returns false: the view has no focused sub-region.
        /// </summary>
        /// <param name="rect">Always <see cref="Rect"/> default.</param>
        /// <returns><c>false</c>.</returns>
        public bool TryGetFocusRect(out Rect rect)
        {
            rect = default(Rect);
            return false;
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            if (available.Width < 1 || available.Height < 1)
                return new Size(0, 0);

            int rows = RowsAt(available.Width).Count;
            return new Size(available.Width, Math.Min(available.Height, rows));
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

            surface.Fill(new Rect(0, 0, width, height), Cell.Blank(Background));
            IReadOnlyList<StyledText> rows = RowsAt(width);
            int maxOffset = Math.Max(0, rows.Count - height);
            if (_ScrollOffset > maxOffset)
                _ScrollOffset = maxOffset;

            for (int y = 0; y < height && _ScrollOffset + y < rows.Count; y++)
                surface.DrawStyledText(0, y, rows[_ScrollOffset + y], Background);
        }

        /// <summary>
        /// Scrolls by <see cref="WheelStep"/> rows on a wheel event. Part of <see cref="IMouseAware"/>.
        /// </summary>
        /// <param name="mouse">The mouse event in view-local coordinates. Must not be null.</param>
        /// <returns><c>true</c> when a wheel event scrolled the view; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mouse"/> is null.</exception>
        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            switch (mouse.Button)
            {
                case MouseButton.WheelUp:
                    _ScrollOffset = Math.Max(0, _ScrollOffset - _WheelStep);
                    return true;
                case MouseButton.WheelDown:
                    _ScrollOffset += _WheelStep;
                    return true;
                default:
                    return false;
            }
        }
    }
}

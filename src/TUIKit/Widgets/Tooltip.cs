namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Theming;
    using TUIKit.Unicode;

    /// <summary>
    /// Draws a small boxed tooltip near an anchor point, clamped to the surface so it never leaves the
    /// screen. Text wraps at <see cref="MaxWidth"/> and may contain newlines. Use it from a render overlay,
    /// or let the host draw it for widgets that implement <see cref="ITooltipProvider"/>.
    /// </summary>
    /// <remarks>Instances are not thread-safe; render from the UI loop.</remarks>
    public sealed class Tooltip : IThemeable
    {
        private int _MaxWidth = 40;

        /// <summary>
        /// Gets or sets the maximum width of the text in columns (the box adds two). Defaults to 40. Must
        /// be at least 1.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set below 1.</exception>
        public int MaxWidth
        {
            get { return _MaxWidth; }
            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Maximum width must be at least 1.");
                _MaxWidth = value;
            }
        }

        /// <summary>
        /// Gets or sets the text style (and box fill). Defaults to black on light grey (palette 7).
        /// </summary>
        public CellStyle Style { get; set; } = CellStyle.Default.WithForeground(Color.FromRgb(0, 0, 0)).WithBackground(Color.FromPalette(7));

        /// <summary>
        /// Gets or sets a value indicating whether a border is drawn. Defaults to false (a padded block).
        /// </summary>
        public bool ShowBorder { get; set; }

        /// <summary>
        /// Applies a theme: the style is <see cref="Theme.Selection"/>.
        /// </summary>
        /// <param name="theme">The theme. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
        public void ApplyTheme(Theme theme)
        {
            if (theme == null)
                throw new ArgumentNullException(nameof(theme));

            Style = theme.Selection;
        }

        /// <summary>
        /// Measures the box that <see cref="Render"/> would draw for some text.
        /// </summary>
        /// <param name="text">The text. Must not be null.</param>
        /// <returns>The box size including padding (or border).</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public Size Measure(string text)
        {
            List<string> lines = Lines(text);
            int width = 0;
            for (int i = 0; i < lines.Count; i++)
                width = Math.Max(width, TextFit.Width(lines[i]));

            return new Size(width + 2, lines.Count + (ShowBorder ? 2 : 0));
        }

        /// <summary>
        /// Draws the tooltip below and to the right of the anchor, flipping above or left when it would
        /// not fit.
        /// </summary>
        /// <param name="surface">The screen surface. Must not be null.</param>
        /// <param name="anchorX">The anchor column (usually the pointer).</param>
        /// <param name="anchorY">The anchor row.</param>
        /// <param name="text">The text. Must not be null.</param>
        /// <returns>The rectangle drawn, or an empty rectangle when nothing fit.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public Rect Render(ISurface surface, int anchorX, int anchorY, string text)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            List<string> lines = Lines(text);
            Size size = Measure(text);
            int screenWidth = surface.Size.Width;
            int screenHeight = surface.Size.Height;
            if (size.Width > screenWidth || size.Height > screenHeight || lines.Count == 0)
                return default;

            int x = anchorX + 1;
            if (x + size.Width > screenWidth)
                x = Math.Max(0, anchorX - size.Width);
            int y = anchorY + 1;
            if (y + size.Height > screenHeight)
                y = Math.Max(0, anchorY - size.Height);

            Rect box = new Rect(x, y, size.Width, size.Height);
            surface.Fill(box, Cell.Blank(Style));
            int textTop = y;
            if (ShowBorder)
            {
                surface.DrawBox(box, Style, BorderStyle.Line);
                textTop = y + 1;
            }

            for (int i = 0; i < lines.Count; i++)
                surface.DrawText(x + 1, textTop + i, lines[i], Style);

            return box;
        }

        private List<string> Lines(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            List<string> result = new List<string>();
            string[] paragraphs = text.Replace("\r\n", "\n").Split('\n');
            for (int p = 0; p < paragraphs.Length; p++)
            {
                IReadOnlyList<StyledText> wrapped = TextWrapper.Wrap(TUIKit.Text.From(paragraphs[p]), _MaxWidth);
                for (int w = 0; w < wrapped.Count; w++)
                    result.Add(wrapped[w].ToPlainString());
            }

            return result;
        }
    }
}

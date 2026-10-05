namespace TUIKit.Widgets
{
    using System;
    using TUIKit;
    using TUIKit.Theming;

    /// <summary>
    /// Draws a frame that shows whether the area inside it holds keyboard focus: heavy lines in the focus
    /// style while focused, the normal border otherwise, and a marker before the focused frame's title.
    /// The host uses it for regions (see <c>TuiApplication.HighlightFocusedRegion</c>), and
    /// <see cref="SplitView.ShowPaneFrames"/> uses it for panes; custom widgets and sub-panes call it so
    /// every pane in an application shows focus the same way.
    /// </summary>
    /// <remarks>
    /// The frame always occupies the outer ring of the rectangle whether focused or not, so focus never
    /// shifts content. Neighbouring frames may share an edge (overlap by one cell): with
    /// <see cref="FocusFrameOptions.JoinBorders"/> they meet in junction glyphs, and the frame drawn last
    /// wins the shared line, so draw unfocused frames first and the focused frame last to keep it whole. Below <see cref="FocusFrameOptions.MinimumBoxSize"/> in either dimension the frame
    /// degrades to a one-column gutter bar at the left edge, drawn only while focused. Stateless and
    /// thread-safe; draw from the render thread.
    /// </remarks>
    public static class FocusFrame
    {
        private const string AsciiGutter = "|";

        /// <summary>
        /// Draws a focus frame whose styles come from a theme: <see cref="Theme.FocusBorderRole"/> and
        /// <see cref="Theme.FocusTitleRole"/> while focused (falling back to bold <see cref="Theme.Accent"/>),
        /// and <see cref="Theme.Border"/> otherwise. With <see cref="Theme.UseAsciiBorders"/> the frame uses
        /// ASCII glyphs, heavy ones while focused.
        /// </summary>
        /// <param name="surface">The target surface. Must not be null.</param>
        /// <param name="rect">The rectangle to frame, in surface coordinates. An empty rectangle draws nothing.</param>
        /// <param name="focused"><c>true</c> when the framed area holds focus.</param>
        /// <param name="theme">The theme supplying the styles. Must not be null.</param>
        /// <param name="options">The frame options. Must not be null.</param>
        /// <param name="title">An optional title for the top edge, or null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="surface"/>,
        /// <paramref name="theme"/>, or <paramref name="options"/> is null.</exception>
        public static void Draw(ISurface surface, Rect rect, bool focused, Theme theme, FocusFrameOptions options, string? title = null)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));
            if (theme == null)
                throw new ArgumentNullException(nameof(theme));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            CellStyle focusedStyle = FocusedStyle(theme);
            CellStyle titleStyle = theme.Resolve(Theme.FocusTitleRole, focusedStyle);
            DrawCore(surface, rect, focused, options.FocusedBorder, options.UnfocusedBorder, focusedStyle, theme.Border, titleStyle, theme.UseAsciiBorders, options, title, options.JoinBorders);
        }

        /// <summary>
        /// Draws a focus frame with explicit styles, for widgets that carry their own style properties
        /// rather than reading a theme.
        /// </summary>
        /// <param name="surface">The target surface. Must not be null.</param>
        /// <param name="rect">The rectangle to frame, in surface coordinates. An empty rectangle draws nothing.</param>
        /// <param name="focused"><c>true</c> when the framed area holds focus.</param>
        /// <param name="focusedStyle">The style of the border and title while focused.</param>
        /// <param name="unfocusedStyle">The style of the border and title while not focused.</param>
        /// <param name="ascii"><c>true</c> to draw ASCII glyphs (heavy ASCII while focused).</param>
        /// <param name="options">The frame options. Must not be null.</param>
        /// <param name="title">An optional title for the top edge, or null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="surface"/> or
        /// <paramref name="options"/> is null.</exception>
        public static void Draw(ISurface surface, Rect rect, bool focused, CellStyle focusedStyle, CellStyle unfocusedStyle, bool ascii, FocusFrameOptions options, string? title = null)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            DrawCore(surface, rect, focused, options.FocusedBorder, options.UnfocusedBorder, focusedStyle, unfocusedStyle, focusedStyle, ascii, options, title, options.JoinBorders);
        }

        /// <summary>
        /// Resolves the focused-frame style from a theme: <see cref="Theme.FocusBorderRole"/> when
        /// registered, otherwise bold <see cref="Theme.Accent"/>.
        /// </summary>
        /// <param name="theme">The theme. Must not be null.</param>
        /// <returns>The style.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
        public static CellStyle FocusedStyle(Theme theme)
        {
            if (theme == null)
                throw new ArgumentNullException(nameof(theme));

            return theme.Resolve(Theme.FocusBorderRole, theme.Accent.WithAttribute(CellAttributes.Bold, true));
        }

        internal static void DrawCore(
            ISurface surface,
            Rect rect,
            bool focused,
            BorderStyle focusedBorder,
            BorderStyle unfocusedBorder,
            CellStyle focusedStyle,
            CellStyle unfocusedStyle,
            CellStyle titleStyle,
            bool ascii,
            FocusFrameOptions options,
            string? title,
            bool join)
        {
            if (rect.Width <= 0 || rect.Height <= 0)
                return;

            int minimum = options.MinimumBoxSize;
            if (rect.Width < minimum || rect.Height < minimum)
            {
                if (focused)
                    surface.Fill(new Rect(rect.X, rect.Y, 1, rect.Height), Cell.Glyph(ascii ? AsciiGutter : options.GutterGlyph, focusedStyle, 1));
                return;
            }

            BorderStyle border;
            if (focused)
                border = ascii ? BorderStyle.AsciiHeavy : (focusedBorder == BorderStyle.None ? BorderStyle.Thick : focusedBorder);
            else
                border = unfocusedBorder == BorderStyle.None ? BorderStyle.None : (ascii ? BorderStyle.Ascii : unfocusedBorder);

            if (border == BorderStyle.None)
                return;

            string? label = title;
            if (focused && !string.IsNullOrEmpty(title) && options.TitleMarker.Length > 0)
                label = options.TitleMarker + title;

            if (join)
                surface.DrawJoinedBox(rect, focused ? focusedStyle : unfocusedStyle, border, label, focused ? titleStyle : unfocusedStyle);
            else if (focused)
                surface.DrawBox(rect, focusedStyle, border, label, titleStyle);
            else
                surface.DrawBox(rect, unfocusedStyle, border, label);
        }
    }
}

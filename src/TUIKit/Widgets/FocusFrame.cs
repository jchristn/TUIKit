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
    /// wins the shared line, so draw unfocused frames first and the focused frame last. By default the focused
    /// frame is drawn whole in its own weight (<see cref="FocusFrameOptions.FocusedJoinMode"/>). Below
    /// <see cref="FocusFrameOptions.MinimumBoxSize"/> in either dimension the frame degrades to a one-column
    /// gutter at the left edge, which writes a distinct glyph while focused and clears its column while not;
    /// below <see cref="FocusFrameOptions.MinimumGutterWidth"/> there is no gutter and focus is shown by a
    /// reverse first column. <see cref="ContentRect"/> returns where content goes in every case. Stateless
    /// and thread-safe; draw from the render thread.
    /// </remarks>
    public static class FocusFrame
    {
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

        /// <summary>
        /// Returns whether a frame drawn in <paramref name="outer"/> degrades to the one-column gutter
        /// instead of a full box: true when the rectangle is smaller than
        /// <see cref="FocusFrameOptions.MinimumBoxSize"/> in either dimension but at least
        /// <see cref="FocusFrameOptions.MinimumGutterWidth"/> wide. Stateless and thread-safe.
        /// </summary>
        /// <param name="outer">The rectangle the frame occupies.</param>
        /// <param name="options">The frame options. Must not be null.</param>
        /// <returns><c>true</c> when the gutter is used; <c>false</c> for a full box, an empty rectangle, or
        /// a rectangle too narrow for a gutter.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
        public static bool UsesGutter(Rect outer, FocusFrameOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            return Mode(outer, options) == FrameMode.Gutter;
        }

        /// <summary>
        /// Returns the area inside a frame drawn in <paramref name="outer"/>, where the framed content
        /// goes: the rectangle inset by one cell on every side for a full box, the rectangle minus its
        /// first column for the gutter, the whole rectangle when it is too narrow for a gutter (see
        /// <see cref="FocusFrameOptions.MinimumGutterWidth"/>), and an empty rectangle for an empty input.
        /// <see cref="Draw(ISurface, Rect, bool, Theme, FocusFrameOptions, string?)"/> never writes inside
        /// it, except the reverse first column of <see cref="ApplyNarrowFocus"/>. Stateless and thread-safe.
        /// </summary>
        /// <param name="outer">The rectangle the frame occupies.</param>
        /// <param name="options">The frame options. Must not be null.</param>
        /// <returns>The content rectangle; may be empty.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
        public static Rect ContentRect(Rect outer, FocusFrameOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            switch (Mode(outer, options))
            {
                case FrameMode.Box:
                    return new Rect(outer.X + 1, outer.Y + 1, outer.Width - 2, outer.Height - 2);
                case FrameMode.Gutter:
                    return new Rect(outer.X + 1, outer.Y, outer.Width - 1, outer.Height);
                case FrameMode.Bare:
                    return outer;
                default:
                    return new Rect(outer.X, outer.Y, 0, 0);
            }
        }

        /// <summary>
        /// Returns the rectangle a frame must occupy to leave <paramref name="content"/> inside it, for
        /// layouts that size content first. The inverse of <see cref="ContentRect"/>: a full box when the
        /// grown rectangle reaches <see cref="FocusFrameOptions.MinimumBoxSize"/> in both dimensions, so
        /// <c>ContentRect(OuterRect(c)) == c</c>; otherwise the gutter form, one column wider. An empty
        /// content rectangle is returned unchanged. Stateless and thread-safe.
        /// </summary>
        /// <param name="content">The content rectangle.</param>
        /// <param name="options">The frame options. Must not be null.</param>
        /// <returns>The outer rectangle.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
        public static Rect OuterRect(Rect content, FocusFrameOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (content.Width <= 0 || content.Height <= 0)
                return content;

            Rect box = new Rect(content.X - 1, content.Y - 1, content.Width + 2, content.Height + 2);
            if (Mode(box, options) == FrameMode.Box)
                return box;

            return new Rect(content.X - 1, content.Y, content.Width + 1, content.Height);
        }

        /// <summary>
        /// Shows focus on a rectangle too narrow for a gutter (narrower than
        /// <see cref="FocusFrameOptions.MinimumGutterWidth"/>): turns on the reverse attribute of every
        /// cell in its first column while focused, keeping the glyphs already there. Call it after the
        /// framed content has rendered, since content drawn later would replace the attribute. Does
        /// nothing for a full box, a gutter, an unfocused frame, or a surface that is not an
        /// <see cref="IReadableSurface"/>. Containers that frame children (<see cref="SplitView"/>,
        /// <c>FramedStack</c>) call it for you. Stateless and thread-safe; draw from the render thread.
        /// </summary>
        /// <param name="surface">The target surface. Must not be null.</param>
        /// <param name="outer">The rectangle the frame occupies.</param>
        /// <param name="focused"><c>true</c> when the framed area holds focus.</param>
        /// <param name="options">The frame options. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="surface"/> or
        /// <paramref name="options"/> is null.</exception>
        public static void ApplyNarrowFocus(ISurface surface, Rect outer, bool focused, FocusFrameOptions options)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (!focused || Mode(outer, options) != FrameMode.Bare || !(surface is IReadableSurface readable))
                return;

            for (int y = outer.Top; y < outer.Bottom; y++)
            {
                Cell cell = readable.Get(outer.X, y);
                if (cell.IsContinuation)
                    continue;

                string glyph = string.IsNullOrEmpty(cell.Grapheme) ? " " : cell.Grapheme;
                surface.Set(outer.X, y, Cell.Glyph(glyph, cell.Style.WithAttribute(CellAttributes.Reverse, true), Math.Max(1, cell.Width)));
            }
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
            FrameMode mode = Mode(rect, options);
            if (mode == FrameMode.Empty)
                return;

            Rect column = new Rect(rect.X, rect.Y, 1, rect.Height);
            if (mode == FrameMode.Bare)
            {
                // Too narrow for a gutter: all of it is content. Mark the first column now; containers
                // repeat it after their content renders (ApplyNarrowFocus).
                if (focused)
                    surface.Fill(column, Cell.Blank(focusedStyle.WithAttribute(CellAttributes.Reverse, true)));
                return;
            }

            if (mode == FrameMode.Gutter)
            {
                string glyph = focused
                    ? (ascii ? options.AsciiFocusedGutterGlyph : options.FocusedGutterGlyph)
                    : (ascii ? options.AsciiUnfocusedGutterGlyph : options.UnfocusedGutterGlyph);
                surface.Fill(column, Cell.Glyph(glyph, focused ? focusedStyle : unfocusedStyle, 1));
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

            CellStyle borderStyle = focused ? focusedStyle : unfocusedStyle;
            CellStyle labelStyle = focused ? titleStyle : unfocusedStyle;
            if (join)
                surface.DrawJoinedBox(rect, borderStyle, border, label, labelStyle, focused ? options.FocusedJoinMode : options.UnfocusedJoinMode, options.TitleAlignment, options.TitleInset);
            else
                surface.DrawBox(rect, borderStyle, border, label, labelStyle, options.TitleAlignment, options.TitleInset);
        }

        private static FrameMode Mode(Rect rect, FocusFrameOptions options)
        {
            if (rect.Width <= 0 || rect.Height <= 0)
                return FrameMode.Empty;

            int minimum = options.MinimumBoxSize;
            if (rect.Width >= minimum && rect.Height >= minimum)
                return FrameMode.Box;

            return rect.Width >= options.MinimumGutterWidth ? FrameMode.Gutter : FrameMode.Bare;
        }
    }
}

namespace TUIKit.Widgets
{
    using System;
    using TUIKit.Unicode;

    /// <summary>
    /// Controls how a focus frame looks (see <see cref="FocusFrame"/>): the border drawn while the framed
    /// area holds focus, the border drawn while it does not, the marker placed before the title of the
    /// focused frame, how frames join their neighbours, where the title sits, and the narrow-space
    /// fallback. Both states reserve the same one-cell border, so moving focus never shifts content.
    /// </summary>
    /// <remarks>
    /// Focus is never shown by color alone: the focused frame always uses different border glyphs
    /// (heavy lines by default, and <see cref="BorderStyle.AsciiHeavy"/> when the theme asks for ASCII
    /// borders), the title marker adds a textual cue, and the narrow gutter uses a different glyph while
    /// focused. Not thread-safe; configure it on the UI thread.
    /// </remarks>
    public sealed class FocusFrameOptions
    {
        private BorderStyle _FocusedBorder = BorderStyle.Thick;
        private BorderStyle _UnfocusedBorder = BorderStyle.Line;
        private string _TitleMarker = "> ";
        private int _MinimumBoxSize = 3;
        private string _FocusedGutterGlyph = "▌";
        private string _AsciiFocusedGutterGlyph = "#";
        private string _UnfocusedGutterGlyph = " ";
        private string _AsciiUnfocusedGutterGlyph = " ";
        private int _MinimumGutterWidth = 1;
        private int _TitleInset = 1;

        /// <summary>
        /// Gets or sets the border drawn while the framed area holds focus. Defaults to
        /// <see cref="BorderStyle.Thick"/> (heavy lines). When the theme uses ASCII borders, the focused
        /// frame is drawn with <see cref="BorderStyle.AsciiHeavy"/> instead, so it still differs by glyph.
        /// Must not be <see cref="BorderStyle.None"/>.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when set to <see cref="BorderStyle.None"/>.</exception>
        public BorderStyle FocusedBorder
        {
            get { return _FocusedBorder; }
            set
            {
                if (value == BorderStyle.None)
                    throw new ArgumentException("A focused frame must draw a border; use a visible border style.", nameof(value));

                _FocusedBorder = value;
            }
        }

        /// <summary>
        /// Gets or sets the border drawn while the framed area does not hold focus. Defaults to
        /// <see cref="BorderStyle.Line"/>. <see cref="BorderStyle.None"/> leaves the reserved cells blank,
        /// so only the focused frame is visible. When the theme uses ASCII borders, any visible style is
        /// drawn as <see cref="BorderStyle.Ascii"/>. The host uses each region's own border instead.
        /// </summary>
        public BorderStyle UnfocusedBorder
        {
            get { return _UnfocusedBorder; }
            set { _UnfocusedBorder = value; }
        }

        /// <summary>
        /// Gets or sets the text placed before the title of the focused frame. Defaults to <c>"&gt; "</c>.
        /// An empty string disables the marker; the glyph change still shows focus. Never null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string TitleMarker
        {
            get { return _TitleMarker; }
            set { _TitleMarker = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>
        /// Gets or sets where the frame title sits on the top edge. Defaults to
        /// <see cref="TUIKit.TitleAlignment.Center"/>, the only placement before 1.5.0. Sidebars, lists,
        /// and logs usually read better with <see cref="TUIKit.TitleAlignment.Left"/>.
        /// </summary>
        public TitleAlignment TitleAlignment { get; set; } = TitleAlignment.Center;

        /// <summary>
        /// Gets or sets the number of border cells kept between a corner and a left- or right-aligned
        /// title. Defaults to 1. Minimum 0 (the title touches the corner), maximum 4. Ignored while
        /// <see cref="TitleAlignment"/> is <see cref="TUIKit.TitleAlignment.Center"/>.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set outside 0 to 4.</exception>
        public int TitleInset
        {
            get { return _TitleInset; }
            set
            {
                if (value < 0 || value > 4)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Title inset must be between 0 and 4.");

                _TitleInset = value;
            }
        }

        /// <summary>
        /// Gets or sets the smallest width and height, in cells, at which a full box is drawn. Below it
        /// the frame degrades to a one-column gutter at the left edge (see <see cref="MinimumGutterWidth"/>).
        /// Defaults to 3. Minimum 2, maximum 64.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set outside 2 to 64.</exception>
        public int MinimumBoxSize
        {
            get { return _MinimumBoxSize; }
            set
            {
                if (value < 2 || value > 64)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Minimum box size must be between 2 and 64.");

                _MinimumBoxSize = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether frames join with box lines already drawn on the
        /// surface, so frames that share an edge or sit inside one another meet in tee, corner, and cross
        /// glyphs instead of overwriting each other (see <see cref="SurfaceExtensions.DrawJoinedBox(ISurface, Rect, CellStyle, BorderStyle, string?, CellStyle, JoinMode)"/>).
        /// How each frame joins is set by <see cref="FocusedJoinMode"/> and <see cref="UnfocusedJoinMode"/>.
        /// Draw unfocused frames first and the focused one last so it stays whole over a shared edge.
        /// <see cref="SplitView.ShowPaneFrames"/> uses it to let its panes share one line. Defaults to false.
        /// </summary>
        public bool JoinBorders { get; set; }

        /// <summary>
        /// Gets or sets how the focused frame joins existing lines while <see cref="JoinBorders"/> is on.
        /// Defaults to <see cref="JoinMode.OverlayWhole"/>: the focused outline is drawn in heavy glyphs
        /// only, with neighbours' lines still connected to it. Set <see cref="JoinMode.Merge"/> for the
        /// 1.4.0 output, where a light neighbour meets the focused frame in mixed junctions such as
        /// <c>┱</c>.
        /// </summary>
        public JoinMode FocusedJoinMode { get; set; } = JoinMode.OverlayWhole;

        /// <summary>
        /// Gets or sets how unfocused frames join existing lines while <see cref="JoinBorders"/> is on.
        /// Defaults to <see cref="JoinMode.Merge"/>.
        /// </summary>
        public JoinMode UnfocusedJoinMode { get; set; } = JoinMode.Merge;

        /// <summary>
        /// Gets or sets the single-cell glyph of the focused narrow-space gutter with Unicode borders. An
        /// alias of <see cref="FocusedGutterGlyph"/>, kept from 1.4.0; prefer the new name.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        /// <exception cref="ArgumentException">Thrown when set to an empty string or a glyph that is not
        /// exactly one cell wide.</exception>
        public string GutterGlyph
        {
            get { return FocusedGutterGlyph; }
            set { FocusedGutterGlyph = value; }
        }

        /// <summary>
        /// Gets or sets the single-cell glyph of the gutter while focused, with Unicode borders. Defaults to
        /// a left half block (<c>▌</c>). Must be exactly one cell wide.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        /// <exception cref="ArgumentException">Thrown when set to an empty string or a glyph that is not
        /// exactly one cell wide.</exception>
        public string FocusedGutterGlyph
        {
            get { return _FocusedGutterGlyph; }
            set { _FocusedGutterGlyph = ValidateGlyph(value); }
        }

        /// <summary>
        /// Gets or sets the single-cell glyph of the gutter while focused, with ASCII borders. Defaults to
        /// <c>#</c>, which differs from every unfocused ASCII line glyph. Before 1.5.0 this was a fixed
        /// <c>|</c>. Must be exactly one cell wide.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        /// <exception cref="ArgumentException">Thrown when set to an empty string or a glyph that is not
        /// exactly one cell wide.</exception>
        public string AsciiFocusedGutterGlyph
        {
            get { return _AsciiFocusedGutterGlyph; }
            set { _AsciiFocusedGutterGlyph = ValidateGlyph(value); }
        }

        /// <summary>
        /// Gets or sets the single-cell glyph of the gutter while not focused, with Unicode borders.
        /// Defaults to a space. The unfocused gutter always writes its column, so nothing from an earlier
        /// frame survives in it. Must be exactly one cell wide.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        /// <exception cref="ArgumentException">Thrown when set to an empty string or a glyph that is not
        /// exactly one cell wide.</exception>
        public string UnfocusedGutterGlyph
        {
            get { return _UnfocusedGutterGlyph; }
            set { _UnfocusedGutterGlyph = ValidateGlyph(value); }
        }

        /// <summary>
        /// Gets or sets the single-cell glyph of the gutter while not focused, with ASCII borders. Defaults
        /// to a space. Must be exactly one cell wide.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        /// <exception cref="ArgumentException">Thrown when set to an empty string or a glyph that is not
        /// exactly one cell wide.</exception>
        public string AsciiUnfocusedGutterGlyph
        {
            get { return _AsciiUnfocusedGutterGlyph; }
            set { _AsciiUnfocusedGutterGlyph = ValidateGlyph(value); }
        }

        /// <summary>
        /// Gets or sets the narrowest rectangle, in columns, that still gets a gutter below
        /// <see cref="MinimumBoxSize"/>. A narrower rectangle draws no gutter: all of it is content, and
        /// focus is shown by a reverse attribute on its first column (see
        /// <see cref="FocusFrame.ApplyNarrowFocus"/>). Defaults to 1, which keeps 1.4.0 behavior (a 1-wide
        /// rectangle is all gutter). Set 2 so 1-wide panes keep their one content column. Minimum 1,
        /// maximum 8.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set outside 1 to 8.</exception>
        public int MinimumGutterWidth
        {
            get { return _MinimumGutterWidth; }
            set
            {
                if (value < 1 || value > 8)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Minimum gutter width must be between 1 and 8.");

                _MinimumGutterWidth = value;
            }
        }

        private static string ValidateGlyph(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (value.Length == 0)
                throw new ArgumentException("Gutter glyph must not be empty.", nameof(value));
            if (TextFit.Width(value) != 1)
                throw new ArgumentException("Gutter glyph must be exactly one cell wide.", nameof(value));

            return value;
        }
    }
}

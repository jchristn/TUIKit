namespace TUIKit.Widgets
{
    using System;

    /// <summary>
    /// Controls how a focus frame looks (see <see cref="FocusFrame"/>): the border drawn while the framed
    /// area holds focus, the border drawn while it does not, the marker placed before the title of the
    /// focused frame, and the narrow-space fallback. Both states reserve the same one-cell border, so
    /// moving focus never shifts content.
    /// </summary>
    /// <remarks>
    /// Focus is never shown by color alone: the focused frame always uses different border glyphs
    /// (heavy lines by default, and <see cref="BorderStyle.AsciiHeavy"/> when the theme asks for ASCII
    /// borders), and the title marker adds a textual cue. Not thread-safe; configure it on the UI thread.
    /// </remarks>
    public sealed class FocusFrameOptions
    {
        private BorderStyle _FocusedBorder = BorderStyle.Thick;
        private BorderStyle _UnfocusedBorder = BorderStyle.Line;
        private string _TitleMarker = "> ";
        private int _MinimumBoxSize = 3;
        private string _GutterGlyph = "▌";

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
        /// Gets or sets the smallest width and height, in cells, at which a full box is drawn. Below it
        /// the frame degrades to a one-column gutter bar at the left edge, drawn only while focused.
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
        /// glyphs instead of overwriting each other (see <see cref="SurfaceExtensions.DrawJoinedBox(ISurface, Rect, CellStyle, BorderStyle, string?, CellStyle)"/>).
        /// Draw unfocused frames first and the focused one last so it stays whole over a shared edge.
        /// <see cref="SplitView.ShowPaneFrames"/> uses it to let its panes share one line. Defaults to false.
        /// </summary>
        public bool JoinBorders { get; set; }

        /// <summary>
        /// Gets or sets the single-cell glyph used for the narrow-space gutter bar. Defaults to a left
        /// half block. With ASCII borders the gutter uses <c>|</c> instead. Must be a non-empty string.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when set to null or empty.</exception>
        public string GutterGlyph
        {
            get { return _GutterGlyph; }
            set
            {
                if (string.IsNullOrEmpty(value))
                    throw new ArgumentException("Gutter glyph must not be null or empty.", nameof(value));

                _GutterGlyph = value;
            }
        }
    }
}

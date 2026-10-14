namespace TUIKit.Content
{
    using System;
    using TUIKit;
    using TUIKit.Unicode;

    /// <summary>
    /// The eleven characters that draw a Markdown table's borders. Use a preset (<see cref="Light"/>,
    /// <see cref="Rounded"/>, <see cref="Heavy"/>, <see cref="Double"/>, <see cref="Ascii"/>, <see cref="None"/>),
    /// map an existing <see cref="BorderStyle"/> with <see cref="FromBorderStyle"/>, or pass your own to the
    /// constructor. Every glyph is exactly one column wide. Instances are immutable and thread-safe.
    /// </summary>
    public sealed class MarkdownTableGlyphs
    {
        private readonly string _Horizontal;
        private readonly string _Vertical;
        private readonly string _TopLeft;
        private readonly string _TopTee;
        private readonly string _TopRight;
        private readonly string _LeftTee;
        private readonly string _Cross;
        private readonly string _RightTee;
        private readonly string _BottomLeft;
        private readonly string _BottomTee;
        private readonly string _BottomRight;

        /// <summary>
        /// Gets the horizontal line glyph. When it is a space, horizontal lines (top, bottom, and separators) are
        /// omitted entirely rather than drawn as blank rows.
        /// </summary>
        public string Horizontal
        {
            get { return _Horizontal; }
        }

        /// <summary>
        /// Gets the vertical line glyph, drawn at the outer edges and between columns.
        /// </summary>
        public string Vertical
        {
            get { return _Vertical; }
        }

        /// <summary>
        /// Gets the top-left corner glyph.
        /// </summary>
        public string TopLeft
        {
            get { return _TopLeft; }
        }

        /// <summary>
        /// Gets the glyph where a column separator meets the top border.
        /// </summary>
        public string TopTee
        {
            get { return _TopTee; }
        }

        /// <summary>
        /// Gets the top-right corner glyph.
        /// </summary>
        public string TopRight
        {
            get { return _TopRight; }
        }

        /// <summary>
        /// Gets the glyph where a separator line meets the left border.
        /// </summary>
        public string LeftTee
        {
            get { return _LeftTee; }
        }

        /// <summary>
        /// Gets the glyph where a separator line crosses a column separator.
        /// </summary>
        public string Cross
        {
            get { return _Cross; }
        }

        /// <summary>
        /// Gets the glyph where a separator line meets the right border.
        /// </summary>
        public string RightTee
        {
            get { return _RightTee; }
        }

        /// <summary>
        /// Gets the bottom-left corner glyph.
        /// </summary>
        public string BottomLeft
        {
            get { return _BottomLeft; }
        }

        /// <summary>
        /// Gets the glyph where a column separator meets the bottom border.
        /// </summary>
        public string BottomTee
        {
            get { return _BottomTee; }
        }

        /// <summary>
        /// Gets the bottom-right corner glyph.
        /// </summary>
        public string BottomRight
        {
            get { return _BottomRight; }
        }

        /// <summary>
        /// Gets light single-line box drawing: <c>┌─┬┐ │ ├┼┤ └┴┘</c>. The default.
        /// </summary>
        public static MarkdownTableGlyphs Light
        {
            get { return new MarkdownTableGlyphs("─", "│", "┌", "┬", "┐", "├", "┼", "┤", "└", "┴", "┘"); }
        }

        /// <summary>
        /// Gets light box drawing with rounded corners: <c>╭─┬╮ │ ├┼┤ ╰┴╯</c>.
        /// </summary>
        public static MarkdownTableGlyphs Rounded
        {
            get { return new MarkdownTableGlyphs("─", "│", "╭", "┬", "╮", "├", "┼", "┤", "╰", "┴", "╯"); }
        }

        /// <summary>
        /// Gets heavy box drawing: <c>┏━┳┓ ┃ ┣╋┫ ┗┻┛</c>.
        /// </summary>
        public static MarkdownTableGlyphs Heavy
        {
            get { return new MarkdownTableGlyphs("━", "┃", "┏", "┳", "┓", "┣", "╋", "┫", "┗", "┻", "┛"); }
        }

        /// <summary>
        /// Gets double-line box drawing: <c>╔═╦╗ ║ ╠╬╣ ╚╩╝</c>.
        /// </summary>
        public static MarkdownTableGlyphs Double
        {
            get { return new MarkdownTableGlyphs("═", "║", "╔", "╦", "╗", "╠", "╬", "╣", "╚", "╩", "╝"); }
        }

        /// <summary>
        /// Gets plain ASCII: <c>+</c> at every corner and junction, <c>-</c> and <c>|</c> for lines. Safe on any
        /// terminal and in logs.
        /// </summary>
        public static MarkdownTableGlyphs Ascii
        {
            get { return new MarkdownTableGlyphs("-", "|", "+", "+", "+", "+", "+", "+", "+", "+", "+"); }
        }

        /// <summary>
        /// Gets spaces for every glyph: columns stay aligned and separated by a space, and horizontal lines are
        /// omitted.
        /// </summary>
        public static MarkdownTableGlyphs None
        {
            get { return new MarkdownTableGlyphs(" ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " "); }
        }

        /// <summary>
        /// Creates a glyph set. Every glyph must be a non-empty string exactly one terminal column wide.
        /// </summary>
        /// <param name="horizontal">The horizontal line glyph; a space omits horizontal lines.</param>
        /// <param name="vertical">The vertical line glyph.</param>
        /// <param name="topLeft">The top-left corner.</param>
        /// <param name="topTee">The top border's column junction.</param>
        /// <param name="topRight">The top-right corner.</param>
        /// <param name="leftTee">The left border's separator junction.</param>
        /// <param name="cross">The inner separator crossing.</param>
        /// <param name="rightTee">The right border's separator junction.</param>
        /// <param name="bottomLeft">The bottom-left corner.</param>
        /// <param name="bottomTee">The bottom border's column junction.</param>
        /// <param name="bottomRight">The bottom-right corner.</param>
        /// <exception cref="ArgumentNullException">Thrown when any glyph is null.</exception>
        /// <exception cref="ArgumentException">Thrown when any glyph is empty or not exactly one column wide.</exception>
        public MarkdownTableGlyphs(
            string horizontal,
            string vertical,
            string topLeft,
            string topTee,
            string topRight,
            string leftTee,
            string cross,
            string rightTee,
            string bottomLeft,
            string bottomTee,
            string bottomRight)
        {
            _Horizontal = Validate(horizontal, nameof(horizontal));
            _Vertical = Validate(vertical, nameof(vertical));
            _TopLeft = Validate(topLeft, nameof(topLeft));
            _TopTee = Validate(topTee, nameof(topTee));
            _TopRight = Validate(topRight, nameof(topRight));
            _LeftTee = Validate(leftTee, nameof(leftTee));
            _Cross = Validate(cross, nameof(cross));
            _RightTee = Validate(rightTee, nameof(rightTee));
            _BottomLeft = Validate(bottomLeft, nameof(bottomLeft));
            _BottomTee = Validate(bottomTee, nameof(bottomTee));
            _BottomRight = Validate(bottomRight, nameof(bottomRight));
        }

        /// <summary>
        /// Maps a <see cref="BorderStyle"/> to the matching glyph set: <see cref="BorderStyle.Line"/> to
        /// <see cref="Light"/>, <see cref="BorderStyle.Rounded"/> to <see cref="Rounded"/>,
        /// <see cref="BorderStyle.Thick"/> to <see cref="Heavy"/>, <see cref="BorderStyle.Double"/> to
        /// <see cref="Double"/>, <see cref="BorderStyle.Ascii"/> and <see cref="BorderStyle.AsciiHeavy"/> to
        /// <see cref="Ascii"/>, and <see cref="BorderStyle.None"/> to <see cref="None"/>. Unknown values map to
        /// <see cref="Light"/>.
        /// </summary>
        /// <param name="style">The border style.</param>
        /// <returns>A new glyph set. Never null.</returns>
        public static MarkdownTableGlyphs FromBorderStyle(BorderStyle style)
        {
            switch (style)
            {
                case BorderStyle.None:
                    return None;
                case BorderStyle.Ascii:
                case BorderStyle.AsciiHeavy:
                    return Ascii;
                case BorderStyle.Rounded:
                    return Rounded;
                case BorderStyle.Double:
                    return Double;
                case BorderStyle.Thick:
                    return Heavy;
                default:
                    return Light;
            }
        }

        private static string Validate(string glyph, string name)
        {
            if (glyph == null)
                throw new ArgumentNullException(name);
            if (glyph.Length == 0)
                throw new ArgumentException("Table glyph '" + name + "' must not be empty.", name);
            if (TextFit.Width(glyph) != 1)
                throw new ArgumentException("Table glyph '" + name + "' must be exactly one column wide; '" + glyph + "' is " + TextFit.Width(glyph) + ".", name);

            return glyph;
        }
    }
}

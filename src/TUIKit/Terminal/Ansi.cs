namespace TUIKit.Terminal
{
    using System.Globalization;
    using System.Text;

    /// <summary>
    /// Builds the ANSI/VT escape sequences TUIKit emits. Sequences are returned as strings so a
    /// backend can write them directly. All members are thread-safe.
    /// </summary>
    public static class Ansi
    {
        /// <summary>
        /// The escape character (0x1B) that begins every control sequence.
        /// </summary>
        public const string Esc = "";

        /// <summary>
        /// The Control Sequence Introducer (ESC followed by '[').
        /// </summary>
        public const string Csi = "[";

        /// <summary>
        /// Gets the sequence that switches to the alternate screen buffer.
        /// </summary>
        public static string EnterAltScreen
        {
            get { return Csi + "?1049h"; }
        }

        /// <summary>
        /// Gets the sequence that returns to the normal screen buffer.
        /// </summary>
        public static string ExitAltScreen
        {
            get { return Csi + "?1049l"; }
        }

        /// <summary>
        /// Gets the sequence that hides the cursor.
        /// </summary>
        public static string HideCursor
        {
            get { return Csi + "?25l"; }
        }

        /// <summary>
        /// Gets the sequence that shows the cursor.
        /// </summary>
        public static string ShowCursor
        {
            get { return Csi + "?25h"; }
        }

        /// <summary>
        /// Gets the sequence that clears the entire screen.
        /// </summary>
        public static string ClearScreen
        {
            get { return Csi + "2J"; }
        }

        /// <summary>
        /// Gets the sequence that resets all SGR attributes.
        /// </summary>
        public static string ResetAttributes
        {
            get { return Csi + "0m"; }
        }

        /// <summary>
        /// Gets the sequence that enables SGR (1006) extended mouse reporting with button and motion
        /// tracking.
        /// </summary>
        public static string EnableMouse
        {
            get { return Csi + "?1000h" + Csi + "?1002h" + Csi + "?1006h"; }
        }

        /// <summary>
        /// Gets the sequence that disables mouse reporting.
        /// </summary>
        public static string DisableMouse
        {
            get { return Csi + "?1006l" + Csi + "?1002l" + Csi + "?1000l"; }
        }

        /// <summary>
        /// Gets the sequence that enables bracketed paste (mode 2004).
        /// </summary>
        public static string EnableBracketedPaste
        {
            get { return Csi + "?2004h"; }
        }

        /// <summary>
        /// Gets the sequence that disables bracketed paste.
        /// </summary>
        public static string DisableBracketedPaste
        {
            get { return Csi + "?2004l"; }
        }

        /// <summary>
        /// Gets the sequence that pushes the Kitty enhanced-keyboard flags, requesting key
        /// disambiguation.
        /// </summary>
        public static string PushKittyKeyboard
        {
            get { return Csi + ">1u"; }
        }

        /// <summary>
        /// Gets the sequence that pops the Kitty enhanced-keyboard flags.
        /// </summary>
        public static string PopKittyKeyboard
        {
            get { return Csi + "<u"; }
        }

        /// <summary>
        /// Builds a cursor-position sequence. Coordinates are one-based in the emitted sequence but
        /// accepted here as zero-based to match the cell buffer.
        /// </summary>
        /// <param name="column">The zero-based column.</param>
        /// <param name="row">The zero-based row.</param>
        /// <returns>The cursor-position sequence.</returns>
        public static string MoveTo(int column, int row)
        {
            return Csi
                + (row + 1).ToString(CultureInfo.InvariantCulture)
                + ";"
                + (column + 1).ToString(CultureInfo.InvariantCulture)
                + "H";
        }

        /// <summary>
        /// Builds the SGR sequence that applies a full <see cref="CellStyle"/>, degrading colors to
        /// the supplied depth.
        /// </summary>
        /// <param name="style">The style to encode.</param>
        /// <param name="depth">The color depth to target.</param>
        /// <returns>The SGR sequence, always beginning with a reset.</returns>
        public static string Sgr(CellStyle style, TerminalColorDepth depth)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(Csi);
            builder.Append('0');

            if (style.HasAttribute(CellAttributes.Bold))
                builder.Append(";1");
            if (style.HasAttribute(CellAttributes.Dim))
                builder.Append(";2");
            if (style.HasAttribute(CellAttributes.Italic))
                builder.Append(";3");
            if (style.HasAttribute(CellAttributes.Underline))
                builder.Append(";4");
            if (style.HasAttribute(CellAttributes.Blink))
                builder.Append(";5");
            if (style.HasAttribute(CellAttributes.Reverse))
                builder.Append(";7");
            if (style.HasAttribute(CellAttributes.Hidden))
                builder.Append(";8");
            if (style.HasAttribute(CellAttributes.Strikethrough))
                builder.Append(";9");

            AppendColor(builder, style.Foreground, depth, true);
            AppendColor(builder, style.Background, depth, false);

            builder.Append('m');
            return builder.ToString();
        }

        /// <summary>
        /// Builds an OSC 8 hyperlink opening sequence.
        /// </summary>
        /// <param name="id">The link identifier, used to associate wrapped runs. May be null or empty.</param>
        /// <param name="uri">The target URI. Must not be null.</param>
        /// <returns>The opening sequence.</returns>
        public static string OpenHyperlink(string? id, string uri)
        {
            string parameters = string.IsNullOrEmpty(id) ? string.Empty : "id=" + id;
            return Esc + "]8;" + parameters + ";" + uri + Esc + "\\";
        }

        /// <summary>
        /// Gets the OSC 8 hyperlink closing sequence.
        /// </summary>
        public static string CloseHyperlink
        {
            get { return Esc + "]8;;" + Esc + "\\"; }
        }

        /// <summary>
        /// Builds an OSC 52 clipboard write sequence.
        /// </summary>
        /// <param name="base64Payload">The base64-encoded clipboard text. Must not be null.</param>
        /// <returns>The clipboard sequence.</returns>
        public static string SetClipboard(string base64Payload)
        {
            return Esc + "]52;c;" + base64Payload + Esc + "\\";
        }

        private static void AppendColor(StringBuilder builder, Color color, TerminalColorDepth depth, bool foreground)
        {
            if (color.Kind == ColorKind.Default)
                return;

            if (color.Kind == ColorKind.Palette)
            {
                AppendPalette(builder, color.PaletteIndex, foreground);
                return;
            }

            switch (depth)
            {
                case TerminalColorDepth.TrueColor:
                    builder.Append(foreground ? ";38;2;" : ";48;2;");
                    builder.Append(color.R.ToString(CultureInfo.InvariantCulture));
                    builder.Append(';');
                    builder.Append(color.G.ToString(CultureInfo.InvariantCulture));
                    builder.Append(';');
                    builder.Append(color.B.ToString(CultureInfo.InvariantCulture));
                    break;
                case TerminalColorDepth.Palette256:
                    AppendPalette(builder, ColorQuantizer.ToPalette256(color.R, color.G, color.B), foreground);
                    break;
                case TerminalColorDepth.Ansi16:
                    AppendAnsi16(builder, ColorQuantizer.ToAnsi16(color.R, color.G, color.B), foreground);
                    break;
                default:
                    break;
            }
        }

        private static void AppendPalette(StringBuilder builder, byte index, bool foreground)
        {
            builder.Append(foreground ? ";38;5;" : ";48;5;");
            builder.Append(index.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendAnsi16(StringBuilder builder, byte index, bool foreground)
        {
            int code;
            if (index < 8)
                code = (foreground ? 30 : 40) + index;
            else
                code = (foreground ? 90 : 100) + (index - 8);

            builder.Append(';');
            builder.Append(code.ToString(CultureInfo.InvariantCulture));
        }
    }
}

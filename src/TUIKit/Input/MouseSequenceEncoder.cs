namespace TUIKit.Input
{
    using System;
    using System.Globalization;

    /// <summary>
    /// Encodes mouse events as the SGR (mode 1006) terminal reports that <see cref="InputParser"/>
    /// decodes, so a test or an automation script can feed real pointer input through the same parser,
    /// hit map, click synthesis, and focus-on-click paths a user exercises. The inverse of the parser's
    /// SGR mouse decoding; see also <see cref="KeySequenceEncoder"/> for keys.
    /// </summary>
    /// <remarks>
    /// Coordinates are zero-based screen cells, as reported by <see cref="MouseEvent"/>; the encoder
    /// adds the one-based wire offset. Stateless and thread-safe.
    /// </remarks>
    public static class MouseSequenceEncoder
    {
        private const string Prefix = "\u001b[<";

        /// <summary>
        /// Encodes a press, release, move, or wheel event as an SGR mouse report.
        /// </summary>
        /// <param name="mouse">The event to encode. Must not be null. Its kind must be
        /// <see cref="MouseEventKind.Press"/>, <see cref="MouseEventKind.Release"/>,
        /// <see cref="MouseEventKind.Move"/>, or <see cref="MouseEventKind.Wheel"/>; enter and leave are
        /// synthesized by the host and have no wire form.</param>
        /// <returns>The escape sequence. Never null or empty.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mouse"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a coordinate is negative.</exception>
        /// <exception cref="ArgumentException">Thrown when the event kind or button has no SGR encoding
        /// (enter, leave, a press or release with no button, or a wheel event without a wheel button).</exception>
        public static string Encode(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));
            if (mouse.X < 0)
                throw new ArgumentOutOfRangeException(nameof(mouse), mouse.X, "Mouse X must be zero or greater.");
            if (mouse.Y < 0)
                throw new ArgumentOutOfRangeException(nameof(mouse), mouse.Y, "Mouse Y must be zero or greater.");

            int code;
            char final = 'M';
            switch (mouse.Kind)
            {
                case MouseEventKind.Press:
                    code = ButtonCode(mouse.Button, false);
                    break;
                case MouseEventKind.Release:
                    code = ButtonCode(mouse.Button, false);
                    final = 'm';
                    break;
                case MouseEventKind.Move:
                    code = 32 + (mouse.Button == MouseButton.None ? 3 : ButtonCode(mouse.Button, false));
                    break;
                case MouseEventKind.Wheel:
                    code = ButtonCode(mouse.Button, true);
                    break;
                default:
                    throw new ArgumentException("Mouse event kind " + mouse.Kind + " has no SGR encoding.", nameof(mouse));
            }

            if ((mouse.Modifiers & KeyModifiers.Shift) != 0)
                code += 4;
            if ((mouse.Modifiers & KeyModifiers.Alt) != 0)
                code += 8;
            if ((mouse.Modifiers & KeyModifiers.Ctrl) != 0)
                code += 16;

            return Prefix
                + code.ToString(CultureInfo.InvariantCulture) + ";"
                + (mouse.X + 1).ToString(CultureInfo.InvariantCulture) + ";"
                + (mouse.Y + 1).ToString(CultureInfo.InvariantCulture) + final;
        }

        /// <summary>
        /// Encodes a left-button press followed by its release at one cell: a single click.
        /// </summary>
        /// <param name="x">The zero-based column. Must be zero or greater.</param>
        /// <param name="y">The zero-based row. Must be zero or greater.</param>
        /// <param name="button">The button. Defaults to <see cref="MouseButton.Left"/>. Must be left,
        /// middle, or right.</param>
        /// <param name="modifiers">Modifier keys held during the click. Defaults to none.</param>
        /// <returns>The press and release sequences, concatenated.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a coordinate is negative.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="button"/> is not a click button.</exception>
        public static string EncodeClick(int x, int y, MouseButton button = MouseButton.Left, KeyModifiers modifiers = KeyModifiers.None)
        {
            return Encode(new MouseEvent(MouseEventKind.Press, button, x, y, modifiers, 1))
                + Encode(new MouseEvent(MouseEventKind.Release, button, x, y, modifiers, 0));
        }

        private static int ButtonCode(MouseButton button, bool wheel)
        {
            if (wheel)
            {
                switch (button)
                {
                    case MouseButton.WheelUp:
                        return 64;
                    case MouseButton.WheelDown:
                        return 65;
                    case MouseButton.WheelLeft:
                        return 66;
                    case MouseButton.WheelRight:
                        return 67;
                    default:
                        throw new ArgumentException("A wheel event needs a wheel button, not " + button + ".", nameof(button));
                }
            }

            switch (button)
            {
                case MouseButton.Left:
                    return 0;
                case MouseButton.Middle:
                    return 1;
                case MouseButton.Right:
                    return 2;
                default:
                    throw new ArgumentException("A press, release, or drag needs the left, middle, or right button, not " + button + ".", nameof(button));
            }
        }
    }
}

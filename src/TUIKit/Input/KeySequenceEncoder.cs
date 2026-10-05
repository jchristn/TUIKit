namespace TUIKit.Input
{
    using System;
    using System.Globalization;
    using System.Text;

    /// <summary>
    /// Encodes key events into the xterm byte sequences a terminal sends, the inverse of
    /// <see cref="InputParser"/>. Used by tests and headless drivers to feed any key (every function key
    /// F1 through F12, modified arrows, Ctrl and Alt chords) into a <see cref="Terminal.HeadlessBackend"/>
    /// without hand-writing escape sequences. All members are thread-safe.
    /// </summary>
    public static class KeySequenceEncoder
    {
        /// <summary>
        /// Encodes a key event.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns>The terminal input sequence. Never null.</returns>
        public static string Encode(KeyEvent key)
        {
            int modifier = 1;
            if ((key.Modifiers & KeyModifiers.Shift) != 0)
                modifier += 1;
            if ((key.Modifiers & KeyModifiers.Alt) != 0)
                modifier += 2;
            if ((key.Modifiers & KeyModifiers.Ctrl) != 0)
                modifier += 4;

            string param = modifier.ToString(CultureInfo.InvariantCulture);
            bool modified = modifier > 1;
            switch (key.Code)
            {
                case KeyCode.Character:
                    return EncodeCharacter(key);
                case KeyCode.Enter:
                    return Alt(key, "\r");
                case KeyCode.Escape:
                    return "\u001b";
                case KeyCode.Tab:
                    return (key.Modifiers & KeyModifiers.Shift) != 0 ? "\u001b[Z" : Alt(key, "\t");
                case KeyCode.Backspace:
                    return Alt(key, "\u007f");
                case KeyCode.Up:
                    return Cursor('A', param, modified);
                case KeyCode.Down:
                    return Cursor('B', param, modified);
                case KeyCode.Right:
                    return Cursor('C', param, modified);
                case KeyCode.Left:
                    return Cursor('D', param, modified);
                case KeyCode.Home:
                    return Cursor('H', param, modified);
                case KeyCode.End:
                    return Cursor('F', param, modified);
                case KeyCode.Insert:
                    return Tilde(2, param, modified);
                case KeyCode.Delete:
                    return Tilde(3, param, modified);
                case KeyCode.PageUp:
                    return Tilde(5, param, modified);
                case KeyCode.PageDown:
                    return Tilde(6, param, modified);
                case KeyCode.F1:
                    return Ss3('P', param, modified);
                case KeyCode.F2:
                    return Ss3('Q', param, modified);
                case KeyCode.F3:
                    return Ss3('R', param, modified);
                case KeyCode.F4:
                    return Ss3('S', param, modified);
                case KeyCode.F5:
                    return Tilde(15, param, modified);
                case KeyCode.F6:
                    return Tilde(17, param, modified);
                case KeyCode.F7:
                    return Tilde(18, param, modified);
                case KeyCode.F8:
                    return Tilde(19, param, modified);
                case KeyCode.F9:
                    return Tilde(20, param, modified);
                case KeyCode.F10:
                    return Tilde(21, param, modified);
                case KeyCode.F11:
                    return Tilde(23, param, modified);
                case KeyCode.F12:
                    return Tilde(24, param, modified);
                default:
                    throw new ArgumentOutOfRangeException(nameof(key), key.Code, "Key code has no terminal encoding.");
            }
        }

        /// <summary>
        /// Encodes a chord written in <see cref="KeyChord.Parse"/> syntax, for example <c>"ctrl+p"</c>,
        /// <c>"f9"</c>, or <c>"shift+tab"</c>.
        /// </summary>
        /// <param name="chord">The chord text. Must not be null or empty.</param>
        /// <returns>The terminal input sequence.</returns>
        /// <exception cref="ArgumentException">Thrown when the chord cannot be parsed.</exception>
        public static string Encode(string chord)
        {
            KeyChord parsed = KeyChord.Parse(chord);
            return Encode(new KeyEvent(parsed.Code, parsed.Rune, parsed.Modifiers));
        }

        private static string EncodeCharacter(KeyEvent key)
        {
            int rune = key.Rune;
            string text;
            if ((key.Modifiers & KeyModifiers.Ctrl) != 0 && rune < 128)
            {
                char c = char.ToLowerInvariant((char)rune);
                if (c >= 'a' && c <= 'z')
                    text = ((char)(c - 'a' + 1)).ToString();
                else if (c == ' ' || c == '@')
                    text = "\0";
                else
                    text = ((char)rune).ToString();
            }
            else
            {
                text = char.ConvertFromUtf32(rune);
                if ((key.Modifiers & KeyModifiers.Shift) != 0 && rune < 128)
                    text = text.ToUpperInvariant();
            }

            return Alt(key, text);
        }

        private static string Alt(KeyEvent key, string text)
        {
            return (key.Modifiers & KeyModifiers.Alt) != 0 ? "\u001b" + text : text;
        }

        private static string Cursor(char final, string param, bool modified)
        {
            return modified ? "\u001b[1;" + param + final : "\u001b[" + final;
        }

        private static string Ss3(char final, string param, bool modified)
        {
            return modified ? "\u001b[1;" + param + final : "\u001bO" + final;
        }

        private static string Tilde(int code, string param, bool modified)
        {
            StringBuilder builder = new StringBuilder("\u001b[");
            builder.Append(code.ToString(CultureInfo.InvariantCulture));
            if (modified)
                builder.Append(';').Append(param);
            builder.Append('~');
            return builder.ToString();
        }
    }
}

namespace TUIKit.Input
{
    using System;
    using System.Globalization;
    using System.Text;

    /// <summary>
    /// A normalized key combination used as the key of the command routing table. A chord can be
    /// created from a <see cref="KeyEvent"/> or parsed from a string such as <c>"ctrl+shift+a"</c>.
    /// ASCII letters are normalized to lower case so bindings are case-insensitive. Instances are
    /// value types and are safe to share across threads.
    /// </summary>
    public readonly struct KeyChord : IEquatable<KeyChord>
    {
        /// <summary>
        /// Gets the key code.
        /// </summary>
        public KeyCode Code { get; }

        /// <summary>
        /// Gets the normalized code point for a character chord, or zero.
        /// </summary>
        public int Rune { get; }

        /// <summary>
        /// Gets the modifiers.
        /// </summary>
        public KeyModifiers Modifiers { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="KeyChord"/> struct.
        /// </summary>
        /// <param name="code">The key code.</param>
        /// <param name="rune">The code point for a character chord, or zero.</param>
        /// <param name="modifiers">The modifiers.</param>
        public KeyChord(KeyCode code, int rune, KeyModifiers modifiers)
        {
            Code = code;
            Rune = Normalize(rune);
            Modifiers = modifiers;
        }

        /// <summary>
        /// Creates a chord from a decoded key event, normalizing the character.
        /// </summary>
        /// <param name="keyEvent">The key event.</param>
        /// <returns>The chord.</returns>
        public static KeyChord FromKeyEvent(KeyEvent keyEvent)
        {
            return new KeyChord(keyEvent.Code, keyEvent.Rune, keyEvent.Modifiers);
        }

        /// <summary>
        /// Parses a chord from a string such as <c>"ctrl+shift+a"</c> or <c>"alt+enter"</c>.
        /// </summary>
        /// <param name="text">The chord text. Must not be null or empty.</param>
        /// <returns>The parsed chord.</returns>
        /// <exception cref="ArgumentException">Thrown when the text is null, empty, or has no key token.</exception>
        /// <exception cref="FormatException">Thrown when a token is not a recognized key or modifier.</exception>
        public static KeyChord Parse(string text)
        {
            if (string.IsNullOrEmpty(text))
                throw new ArgumentException("Chord text must not be null or empty.", nameof(text));

            string[] parts = text.Split('+');
            KeyModifiers modifiers = KeyModifiers.None;
            KeyCode code = KeyCode.Character;
            int rune = 0;
            bool haveKey = false;

            for (int i = 0; i < parts.Length; i++)
            {
                string token = parts[i].Trim().ToLowerInvariant();
                if (token.Length == 0)
                    continue;

                if (TryModifier(token, ref modifiers))
                    continue;

                if (haveKey)
                    throw new FormatException("Chord '" + text + "' has more than one key token.");

                if (TryNamedKey(token, out KeyCode named))
                {
                    code = named;
                    rune = 0;
                }
                else
                {
                    int[] points = ToCodePoints(token);
                    if (points.Length != 1)
                        throw new FormatException("Chord '" + text + "' key token '" + token + "' is not recognized.");

                    code = KeyCode.Character;
                    rune = points[0];
                }

                haveKey = true;
            }

            if (!haveKey)
                throw new ArgumentException("Chord '" + text + "' has no key.", nameof(text));

            return new KeyChord(code, rune, modifiers);
        }

        /// <inheritdoc/>
        public bool Equals(KeyChord other)
        {
            return Code == other.Code && Rune == other.Rune && Modifiers == other.Modifiers;
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is KeyChord other && Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Code;
                hash = (hash * 31) ^ Rune;
                hash = (hash * 31) ^ (int)Modifiers;
                return hash;
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            if ((Modifiers & KeyModifiers.Ctrl) != 0)
                builder.Append("ctrl+");
            if ((Modifiers & KeyModifiers.Alt) != 0)
                builder.Append("alt+");
            if ((Modifiers & KeyModifiers.Shift) != 0)
                builder.Append("shift+");
            if ((Modifiers & KeyModifiers.Super) != 0)
                builder.Append("super+");

            builder.Append(Code == KeyCode.Character ? CharToken() : Code.ToString().ToLowerInvariant());
            return builder.ToString();
        }

        /// <summary>
        /// Formats this chord as a human-readable label suitable for help text, footers, and menus,
        /// using the conventions of the requested <see cref="KeyLabelStyle"/>. For example
        /// <c>ctrl+g</c> renders as <c>Ctrl+G</c> in <see cref="KeyLabelStyle.Ascii"/> and <c>⌃G</c>
        /// in <see cref="KeyLabelStyle.Symbols"/>. Letter chords are shown upper-cased regardless of
        /// the normalized internal casing.
        /// </summary>
        /// <param name="style">The label style to render.</param>
        /// <returns>The formatted label. Never null; empty only for an empty character chord.</returns>
        public string ToLabel(KeyLabelStyle style)
        {
            StringBuilder builder = new StringBuilder();
            AppendModifierLabels(builder, style);
            builder.Append(KeyLabelName(style));
            return builder.ToString();
        }

        private void AppendModifierLabels(StringBuilder builder, KeyLabelStyle style)
        {
            bool symbols = style == KeyLabelStyle.Symbols;
            if ((Modifiers & KeyModifiers.Ctrl) != 0)
                builder.Append(symbols ? "⌃" : "Ctrl+");
            if ((Modifiers & KeyModifiers.Alt) != 0)
                builder.Append(symbols ? "⌥" : "Alt+");
            if ((Modifiers & KeyModifiers.Shift) != 0)
                builder.Append(symbols ? "⇧" : "Shift+");
            if ((Modifiers & KeyModifiers.Super) != 0)
                builder.Append(symbols ? "⌘" : "Super+");
        }

        private string KeyLabelName(KeyLabelStyle style)
        {
            bool symbols = style == KeyLabelStyle.Symbols;
            switch (Code)
            {
                case KeyCode.Character:
                    return CharLabel(symbols);
                case KeyCode.Enter:
                    return symbols ? "⏎" : "Enter";
                case KeyCode.Escape:
                    return "Esc";
                case KeyCode.Tab:
                    return symbols ? "⇥" : "Tab";
                case KeyCode.Backspace:
                    return symbols ? "⌫" : "Backspace";
                case KeyCode.Delete:
                    return symbols ? "⌦" : "Del";
                case KeyCode.Insert:
                    return "Ins";
                case KeyCode.Up:
                    return symbols ? "↑" : "Up";
                case KeyCode.Down:
                    return symbols ? "↓" : "Down";
                case KeyCode.Left:
                    return symbols ? "←" : "Left";
                case KeyCode.Right:
                    return symbols ? "→" : "Right";
                case KeyCode.Home:
                    return "Home";
                case KeyCode.End:
                    return "End";
                case KeyCode.PageUp:
                    return symbols ? "⇞" : "PgUp";
                case KeyCode.PageDown:
                    return symbols ? "⇟" : "PgDn";
                default:
                    return Code.ToString();
            }
        }

        private string CharLabel(bool symbols)
        {
            if (Rune == 0)
                return string.Empty;
            if (Rune == ' ')
                return symbols ? "␣" : "Space";
            if (Rune >= 'a' && Rune <= 'z')
                return ((char)(Rune - ('a' - 'A'))).ToString();

            return char.ConvertFromUtf32(Rune);
        }

        private string CharToken()
        {
            if (Rune == 0)
                return string.Empty;
            if (Rune == ' ')
                return "space";

            return char.ConvertFromUtf32(Rune);
        }

        private static int Normalize(int rune)
        {
            if (rune >= 'A' && rune <= 'Z')
                return rune + ('a' - 'A');

            return rune;
        }

        private static bool TryModifier(string token, ref KeyModifiers modifiers)
        {
            switch (token)
            {
                case "ctrl":
                case "control":
                    modifiers |= KeyModifiers.Ctrl;
                    return true;
                case "alt":
                case "meta":
                case "opt":
                case "option":
                    modifiers |= KeyModifiers.Alt;
                    return true;
                case "shift":
                    modifiers |= KeyModifiers.Shift;
                    return true;
                case "super":
                case "cmd":
                case "command":
                case "win":
                    modifiers |= KeyModifiers.Super;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryNamedKey(string token, out KeyCode code)
        {
            switch (token)
            {
                case "enter":
                case "return":
                    code = KeyCode.Enter;
                    return true;
                case "esc":
                case "escape":
                    code = KeyCode.Escape;
                    return true;
                case "tab":
                    code = KeyCode.Tab;
                    return true;
                case "backspace":
                    code = KeyCode.Backspace;
                    return true;
                case "delete":
                case "del":
                    code = KeyCode.Delete;
                    return true;
                case "insert":
                case "ins":
                    code = KeyCode.Insert;
                    return true;
                case "up":
                    code = KeyCode.Up;
                    return true;
                case "down":
                    code = KeyCode.Down;
                    return true;
                case "left":
                    code = KeyCode.Left;
                    return true;
                case "right":
                    code = KeyCode.Right;
                    return true;
                case "home":
                    code = KeyCode.Home;
                    return true;
                case "end":
                    code = KeyCode.End;
                    return true;
                case "pageup":
                case "pgup":
                    code = KeyCode.PageUp;
                    return true;
                case "pagedown":
                case "pgdn":
                    code = KeyCode.PageDown;
                    return true;
                case "space":
                    code = KeyCode.Character;
                    return false;
                default:
                    return TryFunctionKey(token, out code);
            }
        }

        private static bool TryFunctionKey(string token, out KeyCode code)
        {
            code = KeyCode.Character;
            if (token.Length < 2 || token[0] != 'f')
                return false;

            if (!int.TryParse(token.Substring(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n))
                return false;

            if (n < 1 || n > 12)
                return false;

            code = (KeyCode)(100 + n);
            return true;
        }

        private static int[] ToCodePoints(string token)
        {
            if (token == "space")
                return new[] { (int)' ' };

            int count = 0;
            for (int i = 0; i < token.Length; i++)
            {
                if (char.IsHighSurrogate(token[i]) && i + 1 < token.Length && char.IsLowSurrogate(token[i + 1]))
                    i++;
                count++;
            }

            int[] result = new int[count];
            int index = 0;
            for (int i = 0; i < token.Length; i++)
            {
                if (char.IsHighSurrogate(token[i]) && i + 1 < token.Length && char.IsLowSurrogate(token[i + 1]))
                {
                    result[index++] = char.ConvertToUtf32(token[i], token[i + 1]);
                    i++;
                }
                else
                {
                    result[index++] = token[i];
                }
            }

            return result;
        }
    }
}

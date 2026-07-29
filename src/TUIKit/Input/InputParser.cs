namespace TUIKit.Input
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Decodes a stream of raw terminal input bytes into <see cref="InputEvent"/> values: printable
    /// characters (UTF-8), control keys, CSI and SS3 escape sequences (arrows, navigation, function
    /// keys, CSI u enhanced keys), SGR mouse reports, and bracketed paste. Incomplete sequences are
    /// buffered until more bytes arrive; a lone trailing Escape is resolved by <see cref="Flush"/>.
    /// </summary>
    /// <remarks>Not thread-safe; feed and drain from a single input thread.</remarks>
    public sealed class InputParser
    {
        private readonly List<byte> _Buffer = new List<byte>();
        private readonly StringBuilder _Paste = new StringBuilder();
        private bool _InPaste;

        /// <summary>
        /// Appends raw input bytes to the parser.
        /// </summary>
        /// <param name="data">The buffer containing input bytes. Must not be null.</param>
        /// <param name="length">The number of valid bytes at the start of <paramref name="data"/>.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="length"/> is out of range.</exception>
        public void Feed(byte[] data, int length)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (length < 0 || length > data.Length)
                throw new ArgumentOutOfRangeException(nameof(length));

            for (int i = 0; i < length; i++)
                _Buffer.Add(data[i]);
        }

        /// <summary>
        /// Decodes and returns every complete event currently buffered. Incomplete trailing sequences
        /// remain buffered for the next call.
        /// </summary>
        /// <returns>The decoded events in order. Never null.</returns>
        public IReadOnlyList<InputEvent> Drain()
        {
            List<InputEvent> events = new List<InputEvent>();

            while (_Buffer.Count > 0)
            {
                if (_InPaste)
                {
                    if (!DrainPaste(events))
                        break;

                    continue;
                }

                byte b = _Buffer[0];
                int consumed;

                if (b == 0x1B)
                    consumed = ParseEscape(events);
                else if (b < 0x20 || b == 0x7F)
                    consumed = ParseControl(events);
                else
                    consumed = ParseText(events);

                if (consumed <= 0)
                    break;

                _Buffer.RemoveRange(0, consumed);
            }

            return events;
        }

        /// <summary>
        /// Resolves a lone buffered Escape as an Escape key event. Call this when the input has been
        /// idle past the escape timeout so a bare Escape is not mistaken for the start of a sequence.
        /// </summary>
        /// <returns>The resolved events, typically a single Escape, or empty. Never null.</returns>
        public IReadOnlyList<InputEvent> Flush()
        {
            List<InputEvent> events = new List<InputEvent>();
            if (!_InPaste && _Buffer.Count == 1 && _Buffer[0] == 0x1B)
            {
                events.Add(InputEvent.FromKey(KeyEvent.Special(KeyCode.Escape)));
                _Buffer.Clear();
            }

            return events;
        }

        private bool DrainPaste(List<InputEvent> events)
        {
            // Look for the terminating CSI 201~ sequence (six bytes: ESC [ 2 0 1 ~).
            for (int i = 0; i + 6 <= _Buffer.Count; i++)
            {
                if (_Buffer[i] == 0x1B && Matches(i + 1, "[201~"))
                {
                    for (int j = 0; j < i; j++)
                        _Paste.Append((char)_Buffer[j]);

                    events.Add(InputEvent.FromPaste(DecodeUtf8(_Paste.ToString())));
                    _Paste.Clear();
                    _InPaste = false;
                    _Buffer.RemoveRange(0, i + 6);
                    return true;
                }
            }

            return false;
        }

        private int ParseControl(List<InputEvent> events)
        {
            byte b = _Buffer[0];
            switch (b)
            {
                case 0x0D:
                case 0x0A:
                    events.Add(InputEvent.FromKey(KeyEvent.Special(KeyCode.Enter)));
                    return 1;
                case 0x09:
                    events.Add(InputEvent.FromKey(KeyEvent.Special(KeyCode.Tab)));
                    return 1;
                case 0x08:
                case 0x7F:
                    events.Add(InputEvent.FromKey(KeyEvent.Special(KeyCode.Backspace)));
                    return 1;
                case 0x00:
                    events.Add(InputEvent.FromKey(KeyEvent.Char(' ', KeyModifiers.Ctrl)));
                    return 1;
                default:
                    if (b >= 0x01 && b <= 0x1A)
                    {
                        int letter = 'a' + (b - 1);
                        events.Add(InputEvent.FromKey(KeyEvent.Char(letter, KeyModifiers.Ctrl)));
                        return 1;
                    }

                    return 1; // Consume other C0 controls silently.
            }
        }

        private int ParseText(List<InputEvent> events)
        {
            byte lead = _Buffer[0];
            int length = Utf8Length(lead);
            if (length == 0)
                return 1; // Invalid lead byte; skip.

            if (_Buffer.Count < length)
                return 0; // Need more bytes.

            byte[] bytes = new byte[length];
            for (int i = 0; i < length; i++)
                bytes[i] = _Buffer[i];

            string text = Encoding.UTF8.GetString(bytes);
            if (text.Length > 0)
            {
                int rune = char.ConvertToUtf32(text, 0);
                events.Add(InputEvent.FromKey(KeyEvent.Char(rune)));
            }

            return length;
        }

        private int ParseEscape(List<InputEvent> events)
        {
            if (_Buffer.Count < 2)
                return 0; // Lone ESC; wait for more or Flush.

            byte second = _Buffer[1];
            if (second == '[')
                return ParseCsi(events);
            if (second == 'O')
                return ParseSs3(events);
            if (second == 0x1B)
            {
                events.Add(InputEvent.FromKey(KeyEvent.Special(KeyCode.Escape)));
                return 1;
            }

            // Alt + character.
            int length = Utf8Length(second);
            if (length == 0)
            {
                events.Add(InputEvent.FromKey(KeyEvent.Char(second, KeyModifiers.Alt)));
                return 2;
            }

            if (_Buffer.Count < 1 + length)
                return 0;

            byte[] bytes = new byte[length];
            for (int i = 0; i < length; i++)
                bytes[i] = _Buffer[1 + i];

            string text = Encoding.UTF8.GetString(bytes);
            int rune = text.Length > 0 ? char.ConvertToUtf32(text, 0) : second;
            events.Add(InputEvent.FromKey(KeyEvent.Char(rune, KeyModifiers.Alt)));
            return 1 + length;
        }

        private int ParseCsi(List<InputEvent> events)
        {
            int i = 2;
            bool sgrMouse = i < _Buffer.Count && _Buffer[i] == '<';
            if (sgrMouse)
                i++;

            int paramStart = i;
            while (i < _Buffer.Count && !IsFinalByte(_Buffer[i]))
                i++;

            if (i >= _Buffer.Count)
                return 0; // Sequence not terminated yet.

            byte final = _Buffer[i];
            string paramText = BytesToString(paramStart, i);
            int total = i + 1;

            if (sgrMouse)
            {
                ParseSgrMouse(events, paramText, final);
                return total;
            }

            int[] parameters = ParseParams(paramText);

            // The Kitty keyboard protocol can report a key event type as a sub-parameter of the
            // modifier field (CSI key ; modifier : eventType ...); event type 3 is a key release. A
            // release must not be dispatched as a fresh key press, or it would double-fire commands
            // and dismiss a modal the matching press just opened.
            bool release = IsReleaseEvent(paramText);

            if (final == '~')
            {
                int code = parameters.Length > 0 ? parameters[0] : 0;
                if (code == 200)
                {
                    _InPaste = true;
                    return total;
                }

                KeyModifiers tildeMods = parameters.Length > 1 ? DecodeModifiers(parameters[1]) : KeyModifiers.None;
                KeyCode tilde = MapTilde(code);
                if (!release && tilde != KeyCode.Character)
                    events.Add(InputEvent.FromKey(KeyEvent.Special(tilde, tildeMods)));

                return total;
            }

            if (final == 'u')
            {
                int rune = parameters.Length > 0 ? parameters[0] : 0;
                KeyModifiers uMods = parameters.Length > 1 ? DecodeModifiers(parameters[1]) : KeyModifiers.None;
                if (!release && rune > 0)
                    events.Add(InputEvent.FromKey(KeyEvent.Char(rune, uMods)));

                return total;
            }

            KeyModifiers mods = parameters.Length > 1 ? DecodeModifiers(parameters[1]) : KeyModifiers.None;

            if (final == 'Z')
            {
                if (!release)
                    events.Add(InputEvent.FromKey(KeyEvent.Special(KeyCode.Tab, KeyModifiers.Shift)));

                return total;
            }

            // xterm and the Kitty protocol encode F1–F4 with a CSI final of P/Q/R/S (for example
            // ESC [ P for F1, or ESC [ 1 ; 5 P for Ctrl+F1) in addition to the SS3 form handled above.
            KeyCode functionKey = MapCsiFunctionKey((char)final, parameters);
            if (functionKey != KeyCode.Character)
            {
                if (!release)
                    events.Add(InputEvent.FromKey(KeyEvent.Special(functionKey, mods)));

                return total;
            }

            KeyCode code2 = MapFinal((char)final);
            if (!release && code2 != KeyCode.Character)
                events.Add(InputEvent.FromKey(KeyEvent.Special(code2, mods)));

            return total;
        }

        private int ParseSs3(List<InputEvent> events)
        {
            if (_Buffer.Count < 3)
                return 0;

            byte final = _Buffer[2];
            switch ((char)final)
            {
                case 'A':
                    events.Add(InputEvent.FromKey(KeyEvent.Special(KeyCode.Up)));
                    break;
                case 'B':
                    events.Add(InputEvent.FromKey(KeyEvent.Special(KeyCode.Down)));
                    break;
                case 'C':
                    events.Add(InputEvent.FromKey(KeyEvent.Special(KeyCode.Right)));
                    break;
                case 'D':
                    events.Add(InputEvent.FromKey(KeyEvent.Special(KeyCode.Left)));
                    break;
                case 'H':
                    events.Add(InputEvent.FromKey(KeyEvent.Special(KeyCode.Home)));
                    break;
                case 'F':
                    events.Add(InputEvent.FromKey(KeyEvent.Special(KeyCode.End)));
                    break;
                case 'P':
                    events.Add(InputEvent.FromKey(KeyEvent.Special(KeyCode.F1)));
                    break;
                case 'Q':
                    events.Add(InputEvent.FromKey(KeyEvent.Special(KeyCode.F2)));
                    break;
                case 'R':
                    events.Add(InputEvent.FromKey(KeyEvent.Special(KeyCode.F3)));
                    break;
                case 'S':
                    events.Add(InputEvent.FromKey(KeyEvent.Special(KeyCode.F4)));
                    break;
                default:
                    break;
            }

            return 3;
        }

        private void ParseSgrMouse(List<InputEvent> events, string paramText, byte final)
        {
            int[] parts = ParseParams(paramText);
            if (parts.Length < 3)
                return;

            int b = parts[0];
            int x = parts[1] - 1;
            int y = parts[2] - 1;

            KeyModifiers mods = KeyModifiers.None;
            if ((b & 4) != 0)
                mods |= KeyModifiers.Shift;
            if ((b & 8) != 0)
                mods |= KeyModifiers.Alt;
            if ((b & 16) != 0)
                mods |= KeyModifiers.Ctrl;

            bool motion = (b & 32) != 0;
            bool wheel = (b & 64) != 0;
            int low = b & 3;

            MouseEvent mouse;
            if (wheel)
            {
                MouseButton wheelButton = (b & 1) == 0 ? MouseButton.WheelUp : MouseButton.WheelDown;
                mouse = new MouseEvent(MouseEventKind.Wheel, wheelButton, x, y, mods, 0);
            }
            else if (motion)
            {
                mouse = new MouseEvent(MouseEventKind.Move, ButtonFromLow(low), x, y, mods, 0);
            }
            else
            {
                MouseEventKind kind = final == 'M' ? MouseEventKind.Press : MouseEventKind.Release;
                mouse = new MouseEvent(kind, ButtonFromLow(low), x, y, mods, kind == MouseEventKind.Press ? 1 : 0);
            }

            events.Add(InputEvent.FromMouse(mouse));
        }

        private static MouseButton ButtonFromLow(int low)
        {
            switch (low)
            {
                case 0:
                    return MouseButton.Left;
                case 1:
                    return MouseButton.Middle;
                case 2:
                    return MouseButton.Right;
                default:
                    return MouseButton.None;
            }
        }

        private static KeyCode MapFinal(char final)
        {
            switch (final)
            {
                case 'A':
                    return KeyCode.Up;
                case 'B':
                    return KeyCode.Down;
                case 'C':
                    return KeyCode.Right;
                case 'D':
                    return KeyCode.Left;
                case 'H':
                    return KeyCode.Home;
                case 'F':
                    return KeyCode.End;
                default:
                    return KeyCode.Character;
            }
        }

        private static KeyCode MapCsiFunctionKey(char final, int[] parameters)
        {
            // A leading parameter other than 1 indicates a different sequence that happens to share a
            // final byte — most notably a Cursor Position Report (ESC [ row ; col R) — so only treat
            // P/Q/R/S as F1–F4 when the sequence is unmodified or led by the standard 1.
            int lead = parameters.Length > 0 ? parameters[0] : 0;
            if (lead != 0 && lead != 1)
                return KeyCode.Character;

            switch (final)
            {
                case 'P':
                    return KeyCode.F1;
                case 'Q':
                    return KeyCode.F2;
                case 'R':
                    return KeyCode.F3;
                case 'S':
                    return KeyCode.F4;
                default:
                    return KeyCode.Character;
            }
        }

        private static bool IsReleaseEvent(string paramText)
        {
            if (string.IsNullOrEmpty(paramText))
                return false;

            string[] fields = paramText.Split(';');
            if (fields.Length < 2)
                return false;

            string[] sub = fields[1].Split(':');
            return sub.Length >= 2 && sub[1] == "3";
        }

        private static KeyCode MapTilde(int code)
        {
            switch (code)
            {
                case 1:
                case 7:
                    return KeyCode.Home;
                case 2:
                    return KeyCode.Insert;
                case 3:
                    return KeyCode.Delete;
                case 4:
                case 8:
                    return KeyCode.End;
                case 5:
                    return KeyCode.PageUp;
                case 6:
                    return KeyCode.PageDown;
                case 11:
                    return KeyCode.F1;
                case 12:
                    return KeyCode.F2;
                case 13:
                    return KeyCode.F3;
                case 14:
                    return KeyCode.F4;
                case 15:
                    return KeyCode.F5;
                case 17:
                    return KeyCode.F6;
                case 18:
                    return KeyCode.F7;
                case 19:
                    return KeyCode.F8;
                case 20:
                    return KeyCode.F9;
                case 21:
                    return KeyCode.F10;
                case 23:
                    return KeyCode.F11;
                case 24:
                    return KeyCode.F12;
                default:
                    return KeyCode.Character;
            }
        }

        private static KeyModifiers DecodeModifiers(int param)
        {
            int bits = param - 1;
            if (bits < 0)
                return KeyModifiers.None;

            KeyModifiers mods = KeyModifiers.None;
            if ((bits & 1) != 0)
                mods |= KeyModifiers.Shift;
            if ((bits & 2) != 0)
                mods |= KeyModifiers.Alt;
            if ((bits & 4) != 0)
                mods |= KeyModifiers.Ctrl;
            if ((bits & 8) != 0)
                mods |= KeyModifiers.Super;

            return mods;
        }

        private static int[] ParseParams(string text)
        {
            if (string.IsNullOrEmpty(text))
                return Array.Empty<int>();

            string[] parts = text.Split(';');
            int[] result = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                // A parameter may carry Kitty sub-parameters after a colon (for example the modifier
                // field "5:3"); the primary value is the portion before the first colon.
                string token = parts[i];
                int colon = token.IndexOf(':');
                if (colon >= 0)
                    token = token.Substring(0, colon);

                int value;
                int.TryParse(token, out value);
                result[i] = value;
            }

            return result;
        }

        private string BytesToString(int start, int end)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = start; i < end; i++)
                builder.Append((char)_Buffer[i]);

            return builder.ToString();
        }

        private bool Matches(int offset, string token)
        {
            if (offset + token.Length > _Buffer.Count)
                return false;

            for (int i = 0; i < token.Length; i++)
            {
                if (_Buffer[offset + i] != token[i])
                    return false;
            }

            return true;
        }

        private static string DecodeUtf8(string latin1)
        {
            byte[] bytes = new byte[latin1.Length];
            for (int i = 0; i < latin1.Length; i++)
                bytes[i] = (byte)latin1[i];

            return Encoding.UTF8.GetString(bytes);
        }

        private static bool IsFinalByte(byte b)
        {
            return b >= 0x40 && b <= 0x7E;
        }

        private static int Utf8Length(byte lead)
        {
            if (lead < 0x80)
                return 1;
            if ((lead & 0xE0) == 0xC0)
                return 2;
            if ((lead & 0xF0) == 0xE0)
                return 3;
            if ((lead & 0xF8) == 0xF0)
                return 4;

            return 0;
        }
    }
}

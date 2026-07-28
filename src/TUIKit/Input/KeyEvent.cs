namespace TUIKit.Input
{
    using System;

    /// <summary>
    /// A decoded keyboard event: a key code, an optional printable code point, and the modifiers held.
    /// Instances are value types and are safe to share across threads.
    /// </summary>
    public readonly struct KeyEvent : IEquatable<KeyEvent>
    {
        /// <summary>
        /// Gets the key code. When <see cref="KeyCode.Character"/>, the printable value is in
        /// <see cref="Rune"/>.
        /// </summary>
        public KeyCode Code { get; }

        /// <summary>
        /// Gets the Unicode code point for a character event, or zero for a non-character key.
        /// </summary>
        public int Rune { get; }

        /// <summary>
        /// Gets the modifiers held during the event.
        /// </summary>
        public KeyModifiers Modifiers { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="KeyEvent"/> struct.
        /// </summary>
        /// <param name="code">The key code.</param>
        /// <param name="rune">The Unicode code point for a character event, or zero.</param>
        /// <param name="modifiers">The modifiers held.</param>
        public KeyEvent(KeyCode code, int rune, KeyModifiers modifiers)
        {
            Code = code;
            Rune = rune;
            Modifiers = modifiers;
        }

        /// <summary>
        /// Creates a printable character event.
        /// </summary>
        /// <param name="rune">The Unicode code point.</param>
        /// <param name="modifiers">The modifiers held. Defaults to none.</param>
        /// <returns>The character event.</returns>
        public static KeyEvent Char(int rune, KeyModifiers modifiers = KeyModifiers.None)
        {
            return new KeyEvent(KeyCode.Character, rune, modifiers);
        }

        /// <summary>
        /// Creates a non-character key event.
        /// </summary>
        /// <param name="code">The key code.</param>
        /// <param name="modifiers">The modifiers held. Defaults to none.</param>
        /// <returns>The key event.</returns>
        public static KeyEvent Special(KeyCode code, KeyModifiers modifiers = KeyModifiers.None)
        {
            return new KeyEvent(code, 0, modifiers);
        }

        /// <summary>
        /// Gets the character text for a character event, or an empty string for a non-character key.
        /// </summary>
        /// <returns>The character as a string.</returns>
        public string AsText()
        {
            if (Code != KeyCode.Character || Rune == 0)
                return string.Empty;

            return char.ConvertFromUtf32(Rune);
        }

        /// <inheritdoc/>
        public bool Equals(KeyEvent other)
        {
            return Code == other.Code && Rune == other.Rune && Modifiers == other.Modifiers;
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is KeyEvent other && Equals(other);
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
            return KeyChord.FromKeyEvent(this).ToString();
        }
    }
}

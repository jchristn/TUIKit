namespace TUIKit.Input
{
    using System;

    /// <summary>
    /// One entry for a status bar or help line: a key label and what it does, for example
    /// <c>Enter  Open</c>. Widgets describe their own keys with these (see
    /// <see cref="Widgets.IKeyHintSource"/>), and <see cref="KeyHintResolver"/> merges them along the
    /// focus path so the bar always lists the keys that work right now. Immutable and thread-safe.
    /// </summary>
    public sealed class KeyHint
    {
        /// <summary>
        /// Gets the key label shown to the user, for example <c>Enter</c>, <c>Ctrl+S</c>, or <c>Up/Down</c>.
        /// Hints are de-duplicated by this label (ordinal comparison). Never null or empty.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// Gets what the key does, for example <c>Open</c>. Never null; may be empty.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Gets the ordering weight among hints from the same source: higher shows first. Defaults to 0.
        /// </summary>
        public int Priority { get; }

        /// <summary>
        /// Gets the chord this hint stands for, when it is a single chord, or null for compound labels
        /// such as <c>Up/Down</c>. The resolver uses it to tell whether the key would type text into a
        /// focused text field (see <see cref="KeyChord.InsertsTextWhenTyping"/>).
        /// </summary>
        public KeyChord? Chord { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="KeyHint"/> class with a free-form key label.
        /// </summary>
        /// <param name="key">The key label. Must not be null or empty.</param>
        /// <param name="description">What the key does. Must not be null.</param>
        /// <param name="priority">The ordering weight; higher shows first. Defaults to 0.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null or empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="description"/> is null.</exception>
        public KeyHint(string key, string description, int priority = 0)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key label must not be null or empty.", nameof(key));

            Key = key;
            Description = description ?? throw new ArgumentNullException(nameof(description));
            Priority = priority;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="KeyHint"/> class for a chord, labelled with
        /// <see cref="KeyChord.ToLabel"/> in <see cref="KeyLabelStyle.Ascii"/> style (for example <c>Ctrl+S</c>).
        /// </summary>
        /// <param name="chord">The chord.</param>
        /// <param name="description">What the key does. Must not be null.</param>
        /// <param name="priority">The ordering weight; higher shows first. Defaults to 0.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="description"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the chord has no printable label.</exception>
        public KeyHint(KeyChord chord, string description, int priority = 0)
            : this(chord.ToLabel(KeyLabelStyle.Ascii), description, priority)
        {
            Chord = chord;
        }

        /// <summary>
        /// Gets a value indicating whether the hint stays visible while a text field is typing, even when
        /// its key would type a character (a <c>/</c> command prefix in a chat box). Set it with
        /// <see cref="WhileTyping"/>. Defaults to false.
        /// </summary>
        public bool WorksWhileTyping { get; private set; }

        /// <summary>
        /// Gets the hint shown instead of this one while a text field is typing (for example <c>F1</c>
        /// Help in place of <c>?</c> Help), or null. Set it with <see cref="WithTypingAlternative"/>.
        /// </summary>
        public KeyHint? TypingAlternative { get; private set; }

        /// <summary>
        /// Returns a copy of this hint that stays visible while typing (see <see cref="WorksWhileTyping"/>).
        /// This instance is unchanged.
        /// </summary>
        /// <returns>The new hint.</returns>
        public KeyHint WhileTyping()
        {
            KeyHint copy = Copy();
            copy.WorksWhileTyping = true;
            return copy;
        }

        /// <summary>
        /// Returns a copy of this hint that is replaced by <paramref name="alternative"/> while a text field
        /// is typing. This instance is unchanged.
        /// </summary>
        /// <param name="alternative">The hint shown while typing. Must not be null, and must not be a key
        /// that types text (as decided by <see cref="KeyHintResolver.WouldType"/>; a hint without a parsed
        /// chord is judged by its label).</param>
        /// <returns>The new hint.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="alternative"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="alternative"/> would type text.</exception>
        public KeyHint WithTypingAlternative(KeyHint alternative)
        {
            if (alternative == null)
                throw new ArgumentNullException(nameof(alternative));
            if (KeyHintResolver.WouldType(alternative))
                throw new ArgumentException("A typing alternative must not be a key that types text: " + alternative.Key + ".", nameof(alternative));

            KeyHint copy = Copy();
            copy.TypingAlternative = alternative;
            return copy;
        }

        /// <summary>
        /// Creates a hint from chord text in <see cref="KeyChord.Parse"/> syntax, for example
        /// <c>KeyHint.For("ctrl+s", "Save")</c>.
        /// </summary>
        /// <param name="chord">The chord text. Must not be null or empty.</param>
        /// <param name="description">What the key does. Must not be null.</param>
        /// <param name="priority">The ordering weight; higher shows first. Defaults to 0.</param>
        /// <returns>The hint.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="chord"/> is null, empty, or has no key.</exception>
        /// <exception cref="FormatException">Thrown when a chord token is not recognized.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="description"/> is null.</exception>
        public static KeyHint For(string chord, string description, int priority = 0)
        {
            return new KeyHint(KeyChord.Parse(chord), description, priority);
        }

        private KeyHint Copy()
        {
            KeyHint copy = Chord.HasValue ? new KeyHint(Chord.Value, Description, Priority) : new KeyHint(Key, Description, Priority);
            copy.WorksWhileTyping = WorksWhileTyping;
            copy.TypingAlternative = TypingAlternative;
            return copy;
        }

        /// <summary>
        /// Returns <c>Key Description</c>.
        /// </summary>
        /// <returns>The hint as text. Never null.</returns>
        public override string ToString()
        {
            return Description.Length == 0 ? Key : Key + " " + Description;
        }
    }
}

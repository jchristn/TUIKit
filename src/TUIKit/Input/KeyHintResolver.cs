namespace TUIKit.Input
{
    using System;
    using System.Collections.Generic;
    using TUIKit.Widgets;

    /// <summary>
    /// Works out which keys to advertise for the current focus. It collects hints along a
    /// <see cref="FocusPath"/>, from the focused leaf outward through each enclosing container
    /// (<see cref="IKeyHintSource"/>), then the application-wide hints and commands, and drops later
    /// duplicates of a key so the innermost description wins. When the focused leaf is taking typed text
    /// (<see cref="ITextEntry.AcceptsText"/>), it hides keys that would type a character instead of
    /// running a shortcut and leads with <see cref="LeaveTextHint"/>, so the bar never offers a key that
    /// would land in the text box. Bind it to a status bar with <c>TuiApplication.BindKeyHints</c>.
    /// </summary>
    /// <remarks>
    /// Within one source, hints are ordered by <see cref="KeyHint.Priority"/> (highest first) and then
    /// by the order the source returned them. A hint without a <see cref="KeyHint.Chord"/> whose label is
    /// a single printable character (for example <c>?</c>) is treated as a typing key. Not thread-safe;
    /// configure and resolve on the UI thread.
    /// </remarks>
    public sealed class KeyHintResolver
    {
        private readonly List<KeyHint> _AppHints = new List<KeyHint>();
        private readonly List<CommandRegistry> _Registries = new List<CommandRegistry>();
        private KeyHint _LeaveTextHint = new KeyHint("Tab", "Next field");
        private int _MaxHints = 12;

        /// <summary>
        /// Gets the application-wide hints, listed after every hint from the focus path. Never null.
        /// </summary>
        public IReadOnlyList<KeyHint> AppHints
        {
            get { return _AppHints; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether keys that would insert a character are hidden while
        /// the focused leaf takes typed text. Defaults to true.
        /// </summary>
        public bool HideTypingChords { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether <see cref="LeaveTextHint"/> leads the list while the
        /// focused leaf takes typed text. Defaults to true.
        /// </summary>
        public bool ShowLeaveTextHint { get; set; } = true;

        /// <summary>
        /// Gets or sets the hint that leads the list while the focused leaf takes typed text, telling the
        /// user how to get out of the field. Defaults to <c>Tab  Next field</c>, which matches the host's
        /// focus traversal for every built-in text widget. Never null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public KeyHint LeaveTextHint
        {
            get { return _LeaveTextHint; }
            set { _LeaveTextHint = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>
        /// Gets or sets the most hints returned. Defaults to 12. Minimum 1, maximum 64.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set outside 1 to 64.</exception>
        public int MaxHints
        {
            get { return _MaxHints; }
            set
            {
                if (value < 1 || value > 64)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Maximum hints must be between 1 and 64.");

                _MaxHints = value;
            }
        }

        /// <summary>
        /// Adds an application-wide hint, shown after the focus path's hints whatever holds focus.
        /// </summary>
        /// <param name="hint">The hint. Must not be null.</param>
        /// <returns>This resolver, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="hint"/> is null.</exception>
        public KeyHintResolver AddAppHint(KeyHint hint)
        {
            if (hint == null)
                throw new ArgumentNullException(nameof(hint));

            _AppHints.Add(hint);
            return this;
        }

        /// <summary>
        /// Adds an application-wide hint from chord text, for example <c>AddAppHint("ctrl+q", "Quit")</c>.
        /// </summary>
        /// <param name="chord">The chord text in <see cref="KeyChord.Parse"/> syntax. Must not be null or empty.</param>
        /// <param name="description">What the key does. Must not be null.</param>
        /// <param name="priority">The ordering weight; higher shows first. Defaults to 0.</param>
        /// <returns>This resolver, for chaining.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="chord"/> is null, empty, or has no key.</exception>
        /// <exception cref="FormatException">Thrown when a chord token is not recognized.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="description"/> is null.</exception>
        public KeyHintResolver AddAppHint(string chord, string description, int priority = 0)
        {
            return AddAppHint(KeyHint.For(chord, description, priority));
        }

        /// <summary>
        /// Lists every enabled command in a registry that has a chord as an application-wide hint, using
        /// the command's title. Commands are re-read on every resolve, so enabling or disabling a command
        /// updates the bar.
        /// </summary>
        /// <param name="registry">The registry. Must not be null.</param>
        /// <returns>This resolver, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="registry"/> is null.</exception>
        public KeyHintResolver AddCommands(CommandRegistry registry)
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));

            if (!_Registries.Contains(registry))
                _Registries.Add(registry);
            return this;
        }

        /// <summary>
        /// Removes every application-wide hint and command registry.
        /// </summary>
        public void ClearAppHints()
        {
            _AppHints.Clear();
            _Registries.Clear();
        }

        /// <summary>
        /// Resolves the hints to show for a focus path.
        /// </summary>
        /// <param name="path">The focus path, usually <c>TuiApplication.CurrentFocusPath</c>. Must not be null.</param>
        /// <returns>The hints in display order, at most <see cref="MaxHints"/>. Never null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
        public IReadOnlyList<KeyHint> Resolve(FocusPath path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            bool typing = path.Leaf is ITextEntry entry && entry.AcceptsText;
            List<KeyHint> result = new List<KeyHint>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);

            if (typing && ShowLeaveTextHint)
                Accept(_LeaveTextHint, false, result, seen);

            for (int i = path.Nodes.Count - 1; i >= 0; i--)
            {
                if (path.Nodes[i] is IKeyHintSource source)
                    AddOrdered(source.GetKeyHints(), typing, result, seen);
            }

            AddOrdered(_AppHints, typing, result, seen);
            for (int r = 0; r < _Registries.Count; r++)
            {
                List<KeyHint> commands = new List<KeyHint>();
                IReadOnlyList<Command> list = _Registries[r].Commands;
                for (int c = 0; c < list.Count; c++)
                {
                    Command command = list[c];
                    if (command.Chord.HasValue && command.IsEnabled)
                        commands.Add(new KeyHint(command.Chord.Value, command.Title));
                }

                AddOrdered(commands, typing, result, seen);
            }

            if (result.Count > _MaxHints)
                result.RemoveRange(_MaxHints, result.Count - _MaxHints);

            return result;
        }

        /// <summary>
        /// Determines whether a hint names a key that would insert a character into a text field: its
        /// chord satisfies <see cref="KeyChord.InsertsTextWhenTyping"/>, or, without a chord, its label is
        /// a single printable character.
        /// </summary>
        /// <param name="hint">The hint. Must not be null.</param>
        /// <returns><c>true</c> when the key would type; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="hint"/> is null.</exception>
        public static bool WouldType(KeyHint hint)
        {
            if (hint == null)
                throw new ArgumentNullException(nameof(hint));

            if (hint.Chord.HasValue)
                return hint.Chord.Value.InsertsTextWhenTyping();

            return hint.Key.Length == 1 && !char.IsControl(hint.Key[0]) && !char.IsWhiteSpace(hint.Key[0]);
        }

        private void AddOrdered(IReadOnlyList<KeyHint>? hints, bool typing, List<KeyHint> result, HashSet<string> seen)
        {
            if (hints == null || hints.Count == 0)
                return;

            List<KeyHint> ordered = new List<KeyHint>(hints.Count);
            for (int i = 0; i < hints.Count; i++)
            {
                if (hints[i] != null)
                    ordered.Add(hints[i]);
            }

            StableSortByPriority(ordered);
            for (int i = 0; i < ordered.Count; i++)
                Accept(ordered[i], typing, result, seen);
        }

        private void Accept(KeyHint hint, bool typing, List<KeyHint> result, HashSet<string> seen)
        {
            if (typing && HideTypingChords && WouldType(hint))
                return;
            if (!seen.Add(hint.Key))
                return;

            result.Add(hint);
        }

        private static void StableSortByPriority(List<KeyHint> hints)
        {
            for (int i = 1; i < hints.Count; i++)
            {
                KeyHint current = hints[i];
                int j = i - 1;
                while (j >= 0 && hints[j].Priority < current.Priority)
                {
                    hints[j + 1] = hints[j];
                    j--;
                }

                hints[j + 1] = current;
            }
        }
    }
}

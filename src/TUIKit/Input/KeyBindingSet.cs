namespace TUIKit.Input
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// An editable table of command-to-chord bindings. It backs the <see cref="Widgets.KeyBindingEditor"/>
    /// settings UI and answers "what command does this key run?" for a dispatcher. Rebinding rejects a
    /// chord already claimed by a different command, so the set never holds a conflict.
    /// </summary>
    public sealed class KeyBindingSet
    {
        private readonly List<KeyBinding> _Bindings = new List<KeyBinding>();

        /// <summary>Gets the bindings in insertion order.</summary>
        public IReadOnlyList<KeyBinding> Bindings
        {
            get { return _Bindings; }
        }

        /// <summary>
        /// Adds a binding from a chord string (see <see cref="KeyChord.Parse"/>).
        /// </summary>
        /// <param name="command">The command name. Must not be null.</param>
        /// <param name="chord">The chord text. Must not be null.</param>
        /// <returns>This set, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public KeyBindingSet Add(string command, string chord)
        {
            if (chord == null)
                throw new ArgumentNullException(nameof(chord));

            return Add(command, KeyChord.Parse(chord));
        }

        /// <summary>
        /// Adds a binding.
        /// </summary>
        /// <param name="command">The command name. Must not be null.</param>
        /// <param name="chord">The chord.</param>
        /// <returns>This set, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="command"/> is null.</exception>
        public KeyBindingSet Add(string command, KeyChord chord)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            _Bindings.Add(new KeyBinding(command, chord));
            return this;
        }

        /// <summary>
        /// Finds the binding for a command, or null.
        /// </summary>
        /// <param name="command">The command name. Must not be null.</param>
        /// <returns>The binding or null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="command"/> is null.</exception>
        public KeyBinding? Find(string command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            for (int i = 0; i < _Bindings.Count; i++)
            {
                if (string.Equals(_Bindings[i].Command, command, StringComparison.Ordinal))
                    return _Bindings[i];
            }

            return null;
        }

        /// <summary>
        /// Gets the command bound to a chord, or null when none is.
        /// </summary>
        /// <param name="chord">The chord.</param>
        /// <returns>The command name or null.</returns>
        public string? CommandFor(KeyChord chord)
        {
            for (int i = 0; i < _Bindings.Count; i++)
            {
                if (_Bindings[i].Chord.Equals(chord))
                    return _Bindings[i].Command;
            }

            return null;
        }

        /// <summary>
        /// Rebinds a command to a new chord, unless another command already uses that chord.
        /// </summary>
        /// <param name="command">The command to rebind. Must not be null.</param>
        /// <param name="chord">The new chord.</param>
        /// <returns><c>true</c> when the rebind succeeded; <c>false</c> when the command is unknown or the chord conflicts.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="command"/> is null.</exception>
        public bool Rebind(string command, KeyChord chord)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            KeyBinding? target = Find(command);
            if (target == null)
                return false;

            for (int i = 0; i < _Bindings.Count; i++)
            {
                if (!ReferenceEquals(_Bindings[i], target) && _Bindings[i].Chord.Equals(chord))
                    return false;
            }

            target.Chord = chord;
            return true;
        }
    }
}

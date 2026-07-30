namespace TUIKit.Input
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Maps key chords to command identifiers. Bindings are either global or scoped to a focus
    /// context; a focus-context binding overrides a global binding for the same chord, which is how a
    /// focused pane suppresses a global shortcut it wants for itself. Two-key sequences (for example
    /// <c>Ctrl+K Ctrl+T</c>) are supported at global scope. The table is mutable at runtime.
    /// </summary>
    /// <remarks>All members are thread-safe.</remarks>
    public sealed class CommandRoutingTable
    {
        private readonly object _Sync = new object();
        private readonly Dictionary<KeyChord, string> _Global = new Dictionary<KeyChord, string>();
        private readonly Dictionary<string, Dictionary<KeyChord, string>> _Focus =
            new Dictionary<string, Dictionary<KeyChord, string>>(StringComparer.Ordinal);
        private readonly Dictionary<KeyChord, Dictionary<KeyChord, string>> _Sequences =
            new Dictionary<KeyChord, Dictionary<KeyChord, string>>();
        private ConflictPolicy _ConflictPolicy = ConflictPolicy.Throw;

        /// <summary>
        /// Gets or sets how registration conflicts are handled. Defaults to <see cref="ConflictPolicy.Throw"/>.
        /// </summary>
        public ConflictPolicy ConflictPolicy
        {
            get { lock (_Sync) { return _ConflictPolicy; } }
            set { lock (_Sync) { _ConflictPolicy = value; } }
        }

        /// <summary>
        /// Registers a single-chord binding.
        /// </summary>
        /// <param name="chord">The chord. </param>
        /// <param name="commandId">The command identifier to dispatch. Must not be null or empty.</param>
        /// <param name="scope">The scope. Defaults to <see cref="CommandScope.Global"/>.</param>
        /// <param name="focusContext">The focus context; required when scope is <see cref="CommandScope.FocusContext"/>.</param>
        /// <exception cref="ArgumentException">Thrown when arguments are missing or invalid.</exception>
        /// <exception cref="InvalidOperationException">Thrown on a conflict when the policy is <see cref="ConflictPolicy.Throw"/>.</exception>
        public void Register(KeyChord chord, string commandId, CommandScope scope = CommandScope.Global, string? focusContext = null)
        {
            if (string.IsNullOrEmpty(commandId))
                throw new ArgumentException("Command id must not be null or empty.", nameof(commandId));

            lock (_Sync)
            {
                Dictionary<KeyChord, string> map;
                if (scope == CommandScope.FocusContext)
                {
                    if (string.IsNullOrEmpty(focusContext))
                        throw new ArgumentException("A focus context is required for a focus-scoped binding.", nameof(focusContext));

                    if (!_Focus.TryGetValue(focusContext!, out map!))
                    {
                        map = new Dictionary<KeyChord, string>();
                        _Focus[focusContext!] = map;
                    }
                }
                else
                {
                    map = _Global;
                }

                if (map.ContainsKey(chord) && _ConflictPolicy == ConflictPolicy.Throw)
                    throw new InvalidOperationException("Chord '" + chord + "' is already bound in this scope.");

                map[chord] = commandId;
            }
        }

        /// <summary>
        /// Registers a two-key global sequence binding.
        /// </summary>
        /// <param name="first">The first chord (the prefix).</param>
        /// <param name="second">The second chord.</param>
        /// <param name="commandId">The command identifier. Must not be null or empty.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="commandId"/> is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown on a conflict when the policy is <see cref="ConflictPolicy.Throw"/>.</exception>
        public void RegisterSequence(KeyChord first, KeyChord second, string commandId)
        {
            if (string.IsNullOrEmpty(commandId))
                throw new ArgumentException("Command id must not be null or empty.", nameof(commandId));

            lock (_Sync)
            {
                if (!_Sequences.TryGetValue(first, out Dictionary<KeyChord, string>? map))
                {
                    map = new Dictionary<KeyChord, string>();
                    _Sequences[first] = map;
                }

                if (map.ContainsKey(second) && _ConflictPolicy == ConflictPolicy.Throw)
                    throw new InvalidOperationException("Sequence '" + first + " " + second + "' is already bound.");

                map[second] = commandId;
            }
        }

        /// <summary>
        /// Removes a single-chord binding.
        /// </summary>
        /// <param name="chord">The chord.</param>
        /// <param name="scope">The scope.</param>
        /// <param name="focusContext">The focus context, when scope is focus.</param>
        /// <returns><c>true</c> when a binding was removed; otherwise <c>false</c>.</returns>
        public bool Unregister(KeyChord chord, CommandScope scope = CommandScope.Global, string? focusContext = null)
        {
            lock (_Sync)
            {
                if (scope == CommandScope.FocusContext)
                {
                    if (string.IsNullOrEmpty(focusContext) || !_Focus.TryGetValue(focusContext!, out Dictionary<KeyChord, string>? map))
                        return false;

                    return map.Remove(chord);
                }

                return _Global.Remove(chord);
            }
        }

        /// <summary>
        /// Resolves the command for a single chord, applying focus-over-global precedence.
        /// </summary>
        /// <param name="chord">The chord.</param>
        /// <param name="focusContext">The current focus context, or null.</param>
        /// <returns>The command identifier, or null when the chord is unbound.</returns>
        public string? ResolveSingle(KeyChord chord, string? focusContext)
        {
            lock (_Sync)
            {
                if (!string.IsNullOrEmpty(focusContext)
                    && _Focus.TryGetValue(focusContext!, out Dictionary<KeyChord, string>? focusMap)
                    && focusMap.TryGetValue(chord, out string? focusCommand))
                {
                    return focusCommand;
                }

                if (_Global.TryGetValue(chord, out string? globalCommand))
                    return globalCommand;

                return null;
            }
        }

        /// <summary>
        /// Resolves a chord against only the focus-scoped bindings for the supplied context, ignoring
        /// global bindings. A host uses this to run focus-scoped commands ahead of the focused widget in
        /// an explicit input-precedence chain.
        /// </summary>
        /// <param name="chord">The chord.</param>
        /// <param name="focusContext">The current focus context, or null.</param>
        /// <returns>The command identifier, or null when no focus-scoped binding matches.</returns>
        public string? ResolveFocusScoped(KeyChord chord, string? focusContext)
        {
            if (string.IsNullOrEmpty(focusContext))
                return null;

            lock (_Sync)
            {
                if (_Focus.TryGetValue(focusContext!, out Dictionary<KeyChord, string>? focusMap)
                    && focusMap.TryGetValue(chord, out string? focusCommand))
                {
                    return focusCommand;
                }

                return null;
            }
        }

        /// <summary>
        /// Resolves a chord against only the global bindings, ignoring any focus scope. A host uses this
        /// to run global commands after the focused widget has declined the key.
        /// </summary>
        /// <param name="chord">The chord.</param>
        /// <returns>The command identifier, or null when no global binding matches.</returns>
        public string? ResolveGlobalSingle(KeyChord chord)
        {
            lock (_Sync)
            {
                if (_Global.TryGetValue(chord, out string? globalCommand))
                    return globalCommand;

                return null;
            }
        }

        /// <summary>
        /// Removes a two-key sequence binding. Used to make a rebind idempotent when the conflict policy
        /// is <see cref="ConflictPolicy.Throw"/>.
        /// </summary>
        /// <param name="first">The first chord (the prefix).</param>
        /// <param name="second">The second chord.</param>
        /// <returns><c>true</c> when a binding was removed; otherwise <c>false</c>.</returns>
        public bool UnregisterSequence(KeyChord first, KeyChord second)
        {
            lock (_Sync)
            {
                if (!_Sequences.TryGetValue(first, out Dictionary<KeyChord, string>? map))
                    return false;

                bool removed = map.Remove(second);
                if (map.Count == 0)
                    _Sequences.Remove(first);

                return removed;
            }
        }

        /// <summary>
        /// Determines whether a chord is the prefix of a registered two-key sequence.
        /// </summary>
        /// <param name="chord">The chord.</param>
        /// <returns><c>true</c> when the chord begins a sequence; otherwise <c>false</c>.</returns>
        public bool IsSequencePrefix(KeyChord chord)
        {
            lock (_Sync)
            {
                return _Sequences.ContainsKey(chord);
            }
        }

        /// <summary>
        /// Resolves the command for a completed two-key sequence.
        /// </summary>
        /// <param name="first">The first chord.</param>
        /// <param name="second">The second chord.</param>
        /// <returns>The command identifier, or null when the sequence is unbound.</returns>
        public string? ResolveSequence(KeyChord first, KeyChord second)
        {
            lock (_Sync)
            {
                if (_Sequences.TryGetValue(first, out Dictionary<KeyChord, string>? map)
                    && map.TryGetValue(second, out string? command))
                {
                    return command;
                }

                return null;
            }
        }
    }
}

namespace TUIKit.Input
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// An immutable description of an application command: a stable id, a human title, a category for
    /// grouping, an optional key chord, optional slash aliases, a handler, and an optional enabled
    /// predicate. One command descriptor can drive several surfaces at once — key bindings, a menu bar,
    /// a command palette, and a slash router — through a <see cref="CommandRegistry"/>.
    /// </summary>
    public sealed class Command
    {
        private readonly List<string> _SlashAliases = new List<string>();

        /// <summary>
        /// Initializes a new instance of the <see cref="Command"/> class.
        /// </summary>
        /// <param name="id">The stable command id. Must not be null or empty.</param>
        /// <param name="title">The human-readable title. Must not be null or empty.</param>
        /// <param name="handler">The action invoked when the command runs. Must not be null.</param>
        /// <param name="category">The category used to group the command in menus. Defaults to "General".</param>
        /// <param name="chord">An optional key chord that triggers the command. Defaults to none.</param>
        /// <param name="slashAliases">Optional slash aliases (without the leading "/"). Defaults to none.</param>
        /// <param name="isEnabled">An optional predicate deciding whether the command is currently available; null means always enabled.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> or <paramref name="title"/> is null or empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is null.</exception>
        public Command(string id, string title, Action handler, string category = "General", KeyChord? chord = null, IEnumerable<string>? slashAliases = null, Func<bool>? isEnabled = null)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Command id must not be null or empty.", nameof(id));
            if (string.IsNullOrEmpty(title))
                throw new ArgumentException("Command title must not be null or empty.", nameof(title));

            Id = id;
            Title = title;
            Handler = handler ?? throw new ArgumentNullException(nameof(handler));
            Category = string.IsNullOrEmpty(category) ? "General" : category;
            Chord = chord;
            IsEnabledPredicate = isEnabled;

            if (slashAliases != null)
            {
                foreach (string alias in slashAliases)
                {
                    if (!string.IsNullOrEmpty(alias))
                        _SlashAliases.Add(alias);
                }
            }
        }

        /// <summary>Gets the stable command id. Never null or empty.</summary>
        public string Id { get; }

        /// <summary>Gets the human-readable title. Never null or empty.</summary>
        public string Title { get; }

        /// <summary>Gets the category used to group the command. Never null or empty.</summary>
        public string Category { get; }

        /// <summary>Gets the key chord that triggers the command, or null for none.</summary>
        public KeyChord? Chord { get; }

        /// <summary>Gets the action invoked when the command runs. Never null.</summary>
        public Action Handler { get; }

        /// <summary>Gets the enabled predicate, or null when the command is always enabled.</summary>
        public Func<bool>? IsEnabledPredicate { get; }

        /// <summary>Gets the slash aliases (without the leading "/"). Never null.</summary>
        public IReadOnlyList<string> SlashAliases
        {
            get { return _SlashAliases; }
        }

        /// <summary>
        /// Gets a value indicating whether the command is currently enabled by evaluating its predicate.
        /// </summary>
        public bool IsEnabled
        {
            get { return IsEnabledPredicate == null || IsEnabledPredicate(); }
        }

        /// <summary>
        /// Reports whether the supplied token (without the leading "/") matches this command's id or one
        /// of its slash aliases, case-insensitively.
        /// </summary>
        /// <param name="token">The slash token. Null never matches.</param>
        /// <returns><c>true</c> when it matches; otherwise <c>false</c>.</returns>
        public bool MatchesSlash(string token)
        {
            if (string.IsNullOrEmpty(token))
                return false;

            if (string.Equals(token, Id, StringComparison.OrdinalIgnoreCase))
                return true;

            for (int i = 0; i < _SlashAliases.Count; i++)
            {
                if (string.Equals(token, _SlashAliases[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}

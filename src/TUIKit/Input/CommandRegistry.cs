namespace TUIKit.Input
{
    using System;
    using System.Collections.Generic;
    using TUIKit.Hosting;
    using TUIKit.Widgets;

    /// <summary>
    /// An ordered set of <see cref="Command"/> descriptors that projects onto every command surface from
    /// one registration: it binds chords and handlers into a host, builds a grouped menu bar, builds a
    /// fuzzy command palette, and resolves slash input. Keeping one source of truth means a command
    /// added once appears everywhere, consistently.
    /// </summary>
    public sealed class CommandRegistry
    {
        private readonly List<Command> _Commands = new List<Command>();

        /// <summary>
        /// Gets the registered commands in insertion order. Never null.
        /// </summary>
        public IReadOnlyList<Command> Commands
        {
            get { return _Commands; }
        }

        /// <summary>
        /// Adds a command.
        /// </summary>
        /// <param name="command">The command. Must not be null.</param>
        /// <returns>This registry, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="command"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a command with the same id is already registered.</exception>
        public CommandRegistry Add(Command command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            for (int i = 0; i < _Commands.Count; i++)
            {
                if (string.Equals(_Commands[i].Id, command.Id, StringComparison.Ordinal))
                    throw new ArgumentException("A command with id '" + command.Id + "' is already registered.", nameof(command));
            }

            _Commands.Add(command);
            return this;
        }

        /// <summary>
        /// Registers every command's chord binding and handler with a host application.
        /// </summary>
        /// <param name="app">The host application. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="app"/> is null.</exception>
        public void ApplyTo(TuiApplication app)
        {
            if (app == null)
                throw new ArgumentNullException(nameof(app));

            for (int i = 0; i < _Commands.Count; i++)
            {
                Command command = _Commands[i];
                app.RegisterCommand(command.Id, command.Handler);
                if (command.Chord.HasValue)
                    app.Commands.Register(command.Chord.Value, command.Id);
            }
        }

        /// <summary>
        /// Builds a menu bar with the enabled commands grouped by category in first-seen order.
        /// </summary>
        /// <returns>The menu bar.</returns>
        public MenuBar BuildMenuBar()
        {
            MenuBar bar = new MenuBar();
            Dictionary<string, Menu> menus = new Dictionary<string, Menu>(StringComparer.Ordinal);
            List<string> order = new List<string>();

            for (int i = 0; i < _Commands.Count; i++)
            {
                Command command = _Commands[i];
                if (!command.IsEnabled)
                    continue;

                if (!menus.TryGetValue(command.Category, out Menu? menu))
                {
                    menu = bar.AddMenu(command.Category);
                    menus[command.Category] = menu;
                    order.Add(command.Category);
                }

                Command captured = command;
                menu.Add(command.Title, () => captured.Handler());
            }

            return bar;
        }

        /// <summary>
        /// Builds a fuzzy-finder palette over the enabled commands, matching on their titles.
        /// </summary>
        /// <returns>The palette list.</returns>
        public FuzzyList<Command> BuildPalette()
        {
            List<Command> enabled = new List<Command>();
            for (int i = 0; i < _Commands.Count; i++)
            {
                if (_Commands[i].IsEnabled)
                    enabled.Add(_Commands[i]);
            }

            return new FuzzyList<Command>(enabled, c => c.Title);
        }

        /// <summary>
        /// Resolves slash input to a command. The input may include a leading "/" and trailing arguments;
        /// only the first token is matched, against each command's id and slash aliases.
        /// </summary>
        /// <param name="input">The slash input. Must not be null.</param>
        /// <returns>The matching command, or null when none matches.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> is null.</exception>
        public Command? ResolveSlash(string input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            string trimmed = input.Trim();
            if (trimmed.StartsWith("/", StringComparison.Ordinal))
                trimmed = trimmed.Substring(1);

            int space = trimmed.IndexOf(' ');
            string token = space >= 0 ? trimmed.Substring(0, space) : trimmed;
            if (token.Length == 0)
                return null;

            for (int i = 0; i < _Commands.Count; i++)
            {
                if (_Commands[i].MatchesSlash(token))
                    return _Commands[i];
            }

            return null;
        }
    }
}

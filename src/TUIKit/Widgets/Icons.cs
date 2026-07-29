namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A catalog of common UI icons, each available as a Nerd Font glyph, a portable Unicode symbol,
    /// and an ASCII fallback. Set <see cref="Mode"/> once (typically from a user setting or a terminal
    /// capability probe) and every <see cref="Icon.ToString"/> follows it, so the same code renders
    /// crisp glyphs on a Nerd Font terminal and readable stand-ins over a bare SSH session. The Nerd
    /// glyphs use Font Awesome private-use codepoints, and glyphs are declared with <c>\u</c> escapes
    /// so the source stays ASCII-clean and identical across platforms.
    /// </summary>
    public static class Icons
    {
        private static readonly Dictionary<string, Icon> _ByName = new Dictionary<string, Icon>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Gets or sets the global icon rendering mode. Defaults to <see cref="IconMode.Unicode"/>.</summary>
        public static IconMode Mode { get; set; } = IconMode.Unicode;

        /// <summary>Gets the folder icon.</summary>
        public static Icon Folder { get; } = Register("folder", "", "▸", "[+]");

        /// <summary>Gets the open-folder icon.</summary>
        public static Icon FolderOpen { get; } = Register("folder-open", "", "▾", "[-]");

        /// <summary>Gets the file icon.</summary>
        public static Icon File { get; } = Register("file", "", "◦", "-");

        /// <summary>Gets the git-branch icon.</summary>
        public static Icon GitBranch { get; } = Register("git-branch", "", "⑂", "Y");

        /// <summary>Gets the check / success icon.</summary>
        public static Icon Check { get; } = Register("check", "", "✓", "OK");

        /// <summary>Gets the cross / failure icon.</summary>
        public static Icon Cross { get; } = Register("cross", "", "✗", "X");

        /// <summary>Gets the warning icon.</summary>
        public static Icon Warning { get; } = Register("warning", "", "⚠", "!");

        /// <summary>Gets the information icon.</summary>
        public static Icon Info { get; } = Register("info", "", "ℹ", "i");

        /// <summary>Gets the star icon.</summary>
        public static Icon Star { get; } = Register("star", "", "★", "*");

        /// <summary>Gets the home icon.</summary>
        public static Icon Home { get; } = Register("home", "", "⌂", "~");

        /// <summary>Gets the terminal icon.</summary>
        public static Icon Terminal { get; } = Register("terminal", "", "❯", ">");

        /// <summary>Gets the settings / gear icon.</summary>
        public static Icon Gear { get; } = Register("gear", "", "⚙", "*");

        /// <summary>Gets the search icon.</summary>
        public static Icon Search { get; } = Register("search", "", "⌕", "?");

        /// <summary>Gets the lock icon.</summary>
        public static Icon Lock { get; } = Register("lock", "", "⚿", "#");

        /// <summary>Gets the clock icon.</summary>
        public static Icon Clock { get; } = Register("clock", "", "◷", "o");

        /// <summary>Gets the play icon.</summary>
        public static Icon Play { get; } = Register("play", "", "▶", ">");

        /// <summary>Gets the pause icon.</summary>
        public static Icon Pause { get; } = Register("pause", "", "⏸", "||");

        /// <summary>Gets the right-arrow icon.</summary>
        public static Icon ArrowRight { get; } = Register("arrow-right", "", "→", "->");

        /// <summary>
        /// Looks up an icon by name (case-insensitive), e.g. <c>"folder"</c> or <c>"git-branch"</c>.
        /// </summary>
        /// <param name="name">The icon name. Must not be null.</param>
        /// <returns>The matching icon.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when no icon has the given name.</exception>
        public static Icon Get(string name)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            if (!_ByName.TryGetValue(name, out Icon icon))
                throw new KeyNotFoundException("No icon named '" + name + "'.");

            return icon;
        }

        /// <summary>
        /// Tries to look up an icon by name.
        /// </summary>
        /// <param name="name">The icon name. Must not be null.</param>
        /// <param name="icon">When this method returns, the matching icon or default.</param>
        /// <returns><c>true</c> when found; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
        public static bool TryGet(string name, out Icon icon)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            return _ByName.TryGetValue(name, out icon);
        }

        private static Icon Register(string name, string nerd, string unicode, string ascii)
        {
            Icon icon = new Icon(nerd, unicode, ascii);
            _ByName[name] = icon;
            return icon;
        }
    }
}

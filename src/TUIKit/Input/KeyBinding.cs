namespace TUIKit.Input
{
    using System;

    /// <summary>
    /// Associates a named command with the key chord that triggers it. The chord is mutable so a
    /// <see cref="KeyBindingSet"/> can rebind it from a settings UI.
    /// </summary>
    public sealed class KeyBinding
    {
        /// <summary>Gets the command name.</summary>
        public string Command { get; }

        /// <summary>Gets the currently bound chord.</summary>
        public KeyChord Chord { get; internal set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="KeyBinding"/> class.
        /// </summary>
        /// <param name="command">The command name. Must not be null.</param>
        /// <param name="chord">The bound chord.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="command"/> is null.</exception>
        public KeyBinding(string command, KeyChord chord)
        {
            Command = command ?? throw new ArgumentNullException(nameof(command));
            Chord = chord;
        }
    }
}

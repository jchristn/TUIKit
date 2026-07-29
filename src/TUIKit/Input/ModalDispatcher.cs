namespace TUIKit.Input
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Routes key events through a modal (vi-style) keymap: each <see cref="EditMode"/> has its own
    /// bindings and mode transitions, so the same key does different things in Normal versus Insert
    /// mode. Register per-mode actions with <see cref="Bind"/> and mode changes with
    /// <see cref="Transition"/>; feed keys through <see cref="Handle"/>. Both a bound action and a
    /// transition can fire for one key (the action first), which models commands like <c>:</c> that
    /// act and switch modes at once.
    /// </summary>
    public sealed class ModalDispatcher
    {
        private readonly Dictionary<EditMode, Dictionary<KeyChord, Action>> _Actions = new Dictionary<EditMode, Dictionary<KeyChord, Action>>();
        private readonly Dictionary<EditMode, Dictionary<KeyChord, EditMode>> _Transitions = new Dictionary<EditMode, Dictionary<KeyChord, EditMode>>();

        /// <summary>
        /// Raised after the mode changes, with the new mode.
        /// </summary>
        public event Action<EditMode>? ModeChanged;

        /// <summary>
        /// Gets the current edit mode.
        /// </summary>
        public EditMode Mode { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ModalDispatcher"/> class.
        /// </summary>
        /// <param name="initial">The initial mode.</param>
        public ModalDispatcher(EditMode initial = EditMode.Normal)
        {
            Mode = initial;
        }

        /// <summary>
        /// Binds an action to a key chord within a mode.
        /// </summary>
        /// <param name="mode">The mode in which the binding applies.</param>
        /// <param name="chord">The key chord, e.g. <c>"d"</c>, <c>"Ctrl+S"</c>. Must not be null.</param>
        /// <param name="action">The action to run. Must not be null.</param>
        /// <returns>This dispatcher, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public ModalDispatcher Bind(EditMode mode, string chord, Action action)
        {
            if (chord == null)
                throw new ArgumentNullException(nameof(chord));
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            Map(_Actions, mode)[KeyChord.Parse(chord)] = action;
            return this;
        }

        /// <summary>
        /// Registers a mode transition triggered by a key chord in a mode.
        /// </summary>
        /// <param name="from">The mode the transition applies in.</param>
        /// <param name="chord">The triggering chord. Must not be null.</param>
        /// <param name="to">The mode to switch to.</param>
        /// <returns>This dispatcher, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="chord"/> is null.</exception>
        public ModalDispatcher Transition(EditMode from, string chord, EditMode to)
        {
            if (chord == null)
                throw new ArgumentNullException(nameof(chord));

            Map(_Transitions, from)[KeyChord.Parse(chord)] = to;
            return this;
        }

        /// <summary>
        /// Sets the current mode, raising <see cref="ModeChanged"/> when it actually changes.
        /// </summary>
        /// <param name="mode">The new mode.</param>
        public void SetMode(EditMode mode)
        {
            if (Mode == mode)
                return;

            Mode = mode;
            ModeChanged?.Invoke(mode);
        }

        /// <summary>
        /// Handles a key event against the current mode's bindings and transitions.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns><c>true</c> when a binding or transition matched; otherwise <c>false</c>.</returns>
        public bool Handle(KeyEvent key)
        {
            KeyChord chord = KeyChord.FromKeyEvent(key);
            bool handled = false;

            if (_Actions.TryGetValue(Mode, out Dictionary<KeyChord, Action>? actions)
                && actions.TryGetValue(chord, out Action? action))
            {
                action();
                handled = true;
            }

            if (_Transitions.TryGetValue(Mode, out Dictionary<KeyChord, EditMode>? transitions)
                && transitions.TryGetValue(chord, out EditMode next))
            {
                SetMode(next);
                handled = true;
            }

            return handled;
        }

        private static Dictionary<KeyChord, TValue> Map<TValue>(Dictionary<EditMode, Dictionary<KeyChord, TValue>> outer, EditMode mode)
        {
            if (!outer.TryGetValue(mode, out Dictionary<KeyChord, TValue>? inner))
            {
                inner = new Dictionary<KeyChord, TValue>();
                outer[mode] = inner;
            }

            return inner;
        }
    }
}

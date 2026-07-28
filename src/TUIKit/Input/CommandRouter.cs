namespace TUIKit.Input
{
    using System;

    /// <summary>
    /// Routes decoded keys through a <see cref="CommandRoutingTable"/>, tracking the pending state of
    /// multi-key sequences. When a key begins a sequence the router reports
    /// <see cref="CommandResolutionStatus.Pending"/>; the next key completes or abandons it. A pending
    /// sequence should be cleared with <see cref="ResetPending"/> after the sequence timeout elapses.
    /// </summary>
    /// <remarks>All members are thread-safe.</remarks>
    public sealed class CommandRouter
    {
        private readonly object _Sync = new object();
        private readonly CommandRoutingTable _Table;
        private KeyChord _PendingFirst;
        private bool _HasPending;

        /// <summary>
        /// Gets the routing table used by this router.
        /// </summary>
        public CommandRoutingTable Table
        {
            get { return _Table; }
        }

        /// <summary>
        /// Gets a value indicating whether the router is waiting for the second key of a sequence.
        /// </summary>
        public bool HasPending
        {
            get { lock (_Sync) { return _HasPending; } }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandRouter"/> class.
        /// </summary>
        /// <param name="table">The routing table. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="table"/> is null.</exception>
        public CommandRouter(CommandRoutingTable table)
        {
            _Table = table ?? throw new ArgumentNullException(nameof(table));
        }

        /// <summary>
        /// Routes a key event.
        /// </summary>
        /// <param name="keyEvent">The key event.</param>
        /// <param name="focusContext">The current focus context, or null.</param>
        /// <returns>The resolution.</returns>
        public CommandResolution Process(KeyEvent keyEvent, string? focusContext)
        {
            KeyChord chord = KeyChord.FromKeyEvent(keyEvent);

            lock (_Sync)
            {
                if (_HasPending)
                {
                    _HasPending = false;
                    string? sequenceCommand = _Table.ResolveSequence(_PendingFirst, chord);
                    if (sequenceCommand != null)
                        return new CommandResolution(CommandResolutionStatus.Command, sequenceCommand);

                    return new CommandResolution(CommandResolutionStatus.None, null);
                }

                if (_Table.IsSequencePrefix(chord))
                {
                    _PendingFirst = chord;
                    _HasPending = true;
                    return new CommandResolution(CommandResolutionStatus.Pending, null);
                }

                string? command = _Table.ResolveSingle(chord, focusContext);
                if (command != null)
                    return new CommandResolution(CommandResolutionStatus.Command, command);

                return new CommandResolution(CommandResolutionStatus.None, null);
            }
        }

        /// <summary>
        /// Clears any pending multi-key sequence, typically after the sequence timeout elapses.
        /// </summary>
        public void ResetPending()
        {
            lock (_Sync)
            {
                _HasPending = false;
            }
        }
    }
}

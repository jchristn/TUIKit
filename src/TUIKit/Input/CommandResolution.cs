namespace TUIKit.Input
{
    /// <summary>
    /// The result of routing a key: whether it was consumed, and the command it resolved to.
    /// </summary>
    public sealed class CommandResolution
    {
        /// <summary>
        /// Gets the resolution status.
        /// </summary>
        public CommandResolutionStatus Status { get; }

        /// <summary>
        /// Gets the resolved command identifier when <see cref="Status"/> is
        /// <see cref="CommandResolutionStatus.Command"/>; otherwise null.
        /// </summary>
        public string? CommandId { get; }

        /// <summary>
        /// Gets a value indicating whether the key was consumed by routing (a command or a pending
        /// sequence step) and should not fall through to text input.
        /// </summary>
        public bool Consumed
        {
            get { return Status != CommandResolutionStatus.None; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandResolution"/> class.
        /// </summary>
        /// <param name="status">The status.</param>
        /// <param name="commandId">The resolved command identifier, or null.</param>
        public CommandResolution(CommandResolutionStatus status, string? commandId)
        {
            Status = status;
            CommandId = commandId;
        }
    }
}

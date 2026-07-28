namespace TUIKit.Input
{
    /// <summary>
    /// The outcome of routing a key through the <see cref="CommandRouter"/>.
    /// </summary>
    public enum CommandResolutionStatus
    {
        /// <summary>The key matched no binding and was not consumed.</summary>
        None = 0,

        /// <summary>The key began a multi-key sequence; the router is waiting for the next key.</summary>
        Pending = 1,

        /// <summary>The key resolved to a command.</summary>
        Command = 2
    }
}

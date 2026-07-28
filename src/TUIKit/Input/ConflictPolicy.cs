namespace TUIKit.Input
{
    /// <summary>
    /// How the routing table reacts when a chord is registered that is already bound in the same scope.
    /// </summary>
    public enum ConflictPolicy
    {
        /// <summary>Throw an exception on the conflicting registration. The default.</summary>
        Throw = 0,

        /// <summary>Silently replace the existing binding.</summary>
        LastWins = 1
    }
}

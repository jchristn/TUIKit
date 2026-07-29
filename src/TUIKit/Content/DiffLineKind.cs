namespace TUIKit.Content
{
    /// <summary>
    /// The role of a line in a rendered diff.
    /// </summary>
    public enum DiffLineKind
    {
        /// <summary>Unchanged context, present in both sides.</summary>
        Context = 0,

        /// <summary>A line added on the new side.</summary>
        Added = 1,

        /// <summary>A line removed from the old side.</summary>
        Removed = 2
    }
}

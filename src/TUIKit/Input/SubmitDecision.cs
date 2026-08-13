namespace TUIKit.Input
{
    /// <summary>
    /// The outcome of resolving a key pressed inside a multi-line editor: whether it submits the
    /// content, inserts a newline, or is not a submit-related key at all.
    /// </summary>
    public enum SubmitDecision
    {
        /// <summary>The key is unrelated to submission; the editor should handle it normally.</summary>
        Ignore = 0,

        /// <summary>The key submits the content.</summary>
        Submit = 1,

        /// <summary>The key inserts a newline into the content.</summary>
        InsertNewline = 2
    }
}

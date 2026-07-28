namespace TUIKit.Input
{
    /// <summary>
    /// The kind of a decoded <see cref="InputEvent"/>.
    /// </summary>
    public enum InputEventKind
    {
        /// <summary>A keyboard event.</summary>
        Key = 0,

        /// <summary>A bracketed-paste event carrying literal pasted text.</summary>
        Paste = 1,

        /// <summary>A mouse event.</summary>
        Mouse = 2
    }
}

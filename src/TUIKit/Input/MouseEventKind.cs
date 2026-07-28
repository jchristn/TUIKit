namespace TUIKit.Input
{
    /// <summary>
    /// The kind of a mouse event.
    /// </summary>
    public enum MouseEventKind
    {
        /// <summary>A button was pressed.</summary>
        Press = 0,

        /// <summary>A button was released.</summary>
        Release = 1,

        /// <summary>The pointer moved (optionally with a button held).</summary>
        Move = 2,

        /// <summary>The scroll wheel moved.</summary>
        Wheel = 3
    }
}

namespace TUIKit.Input
{
    /// <summary>
    /// Identifies which mouse button an event concerns.
    /// </summary>
    public enum MouseButton
    {
        /// <summary>No button (motion only).</summary>
        None = 0,

        /// <summary>The left button.</summary>
        Left = 1,

        /// <summary>The middle button.</summary>
        Middle = 2,

        /// <summary>The right button.</summary>
        Right = 3,

        /// <summary>The scroll wheel moving up.</summary>
        WheelUp = 4,

        /// <summary>The scroll wheel moving down.</summary>
        WheelDown = 5,

        /// <summary>The scroll wheel tilting or scrolling left (SGR button 66).</summary>
        WheelLeft = 6,

        /// <summary>The scroll wheel tilting or scrolling right (SGR button 67).</summary>
        WheelRight = 7
    }
}

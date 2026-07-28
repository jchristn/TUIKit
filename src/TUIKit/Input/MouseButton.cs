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
        WheelDown = 5
    }
}

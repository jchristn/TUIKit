namespace TUIKit.Input
{
    /// <summary>
    /// Selects how much pointer traffic the terminal is asked to report while mouse capture is
    /// enabled. Richer modes are supersets: every mode includes everything below it.
    /// </summary>
    public enum MouseTrackingMode
    {
        /// <summary>No mouse reporting at all; the application receives no mouse events.</summary>
        None = 0,

        /// <summary>
        /// Button presses, releases, the wheel, and motion only while a button is held (drag).
        /// Hover Enter/Leave events fire only during drags. (DECSET 1000 + 1002 + 1006.)
        /// </summary>
        ButtonsAndDrag = 1,

        /// <summary>
        /// Everything in <see cref="ButtonsAndDrag"/> plus motion with no button held, enabling hover
        /// tracking. The default. Terminals without any-motion support (DECSET 1003) silently degrade
        /// to <see cref="ButtonsAndDrag"/> behavior. (DECSET 1000 + 1002 + 1003 + 1006.)
        /// </summary>
        AnyMotion = 2
    }
}

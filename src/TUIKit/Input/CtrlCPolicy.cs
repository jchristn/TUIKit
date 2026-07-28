namespace TUIKit.Input
{
    /// <summary>
    /// How <c>Ctrl+C</c> is handled by the host. Chosen by the application because the right behavior
    /// differs by app: some want it to quit, some want it to interrupt the focused pane, and some
    /// require pressing it twice.
    /// </summary>
    public enum CtrlCPolicy
    {
        /// <summary>Ctrl+C ends the application.</summary>
        Kill = 0,

        /// <summary>Ctrl+C raises an interrupt for the focused pane and does not quit.</summary>
        InterruptFocusedPane = 1,

        /// <summary>Ctrl+C must be pressed twice within a short window to quit.</summary>
        DoubleTapToExit = 2,

        /// <summary>Ctrl+C is delivered to the application as an ordinary routable event with no built-in behavior.</summary>
        Custom = 3
    }
}

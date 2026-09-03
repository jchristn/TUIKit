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
        Wheel = 3,

        /// <summary>
        /// The pointer entered a widget's bounds. Synthesized by the host from hit-test transitions;
        /// never produced by the input parser. Delivered before the event that caused the transition.
        /// </summary>
        Enter = 4,

        /// <summary>
        /// The pointer left a widget's bounds (moved to another widget, to unbound screen area, or the
        /// terminal lost focus). Synthesized by the host; never produced by the input parser.
        /// </summary>
        Leave = 5
    }
}

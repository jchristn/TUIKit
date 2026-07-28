namespace TUIKit.Input
{
    /// <summary>
    /// A decoded mouse event in absolute, zero-based cell coordinates. Click counts (single, double,
    /// triple) are synthesized by higher layers from press timing. Instances are immutable.
    /// </summary>
    public sealed class MouseEvent
    {
        /// <summary>
        /// Gets the kind of mouse event.
        /// </summary>
        public MouseEventKind Kind { get; }

        /// <summary>
        /// Gets the button involved.
        /// </summary>
        public MouseButton Button { get; }

        /// <summary>
        /// Gets the zero-based column.
        /// </summary>
        public int X { get; }

        /// <summary>
        /// Gets the zero-based row.
        /// </summary>
        public int Y { get; }

        /// <summary>
        /// Gets the modifiers held during the event.
        /// </summary>
        public KeyModifiers Modifiers { get; }

        /// <summary>
        /// Gets the synthesized click count (1 for single, 2 for double, 3 for triple), or zero when
        /// not applicable.
        /// </summary>
        public int ClickCount { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MouseEvent"/> class.
        /// </summary>
        /// <param name="kind">The kind of event.</param>
        /// <param name="button">The button involved.</param>
        /// <param name="x">The zero-based column.</param>
        /// <param name="y">The zero-based row.</param>
        /// <param name="modifiers">The modifiers held.</param>
        /// <param name="clickCount">The synthesized click count, or zero.</param>
        public MouseEvent(MouseEventKind kind, MouseButton button, int x, int y, KeyModifiers modifiers, int clickCount)
        {
            Kind = kind;
            Button = button;
            X = x;
            Y = y;
            Modifiers = modifiers;
            ClickCount = clickCount;
        }

        /// <summary>
        /// Returns a copy of this event with a different click count.
        /// </summary>
        /// <param name="clickCount">The new click count.</param>
        /// <returns>The derived event.</returns>
        public MouseEvent WithClickCount(int clickCount)
        {
            return new MouseEvent(Kind, Button, X, Y, Modifiers, clickCount);
        }
    }
}

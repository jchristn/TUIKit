namespace TUIKit.Widgets
{
    /// <summary>
    /// One clickable area recorded during a render pass (see <see cref="ClickRegionMap{TAction}"/>): the
    /// rectangle, the action it stands for, and optional key and tooltip text. Immutable.
    /// </summary>
    /// <typeparam name="TAction">The application's action type, for example an enum or a record.</typeparam>
    public sealed class ClickRegion<TAction>
    {
        /// <summary>
        /// Gets the clickable rectangle, in the coordinates of the surface it was drawn on (the widget's
        /// own coordinates when recorded during the widget's render).
        /// </summary>
        public Rect Area { get; }

        /// <summary>
        /// Gets the action this area stands for.
        /// </summary>
        public TAction Action { get; }

        /// <summary>
        /// Gets the key that does the same thing from the keyboard, or null. Every clickable action
        /// should have one so the interface works without a mouse.
        /// </summary>
        public string? Key { get; }

        /// <summary>
        /// Gets the tooltip text, or null.
        /// </summary>
        public string? Tooltip { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClickRegion{TAction}"/> class.
        /// </summary>
        /// <param name="area">The clickable rectangle.</param>
        /// <param name="action">The action.</param>
        /// <param name="key">The equivalent key, or null.</param>
        /// <param name="tooltip">The tooltip text, or null.</param>
        public ClickRegion(Rect area, TAction action, string? key = null, string? tooltip = null)
        {
            Area = area;
            Action = action;
            Key = key;
            Tooltip = tooltip;
        }
    }
}

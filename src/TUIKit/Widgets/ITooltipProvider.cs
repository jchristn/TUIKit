namespace TUIKit.Widgets
{
    /// <summary>
    /// Implemented by widgets that show a tooltip when the pointer rests over them. The host asks for the
    /// tooltip after the pointer has been still over the widget for
    /// <see cref="Hosting.TuiApplication.TooltipDelayMilliseconds"/> and draws it with <see cref="Tooltip"/>
    /// near the pointer. Containers may forward the query to the child under the pointer.
    /// </summary>
    public interface ITooltipProvider
    {
        /// <summary>
        /// Gets the tooltip text for a widget-local position, or null for none.
        /// </summary>
        /// <param name="x">The column, relative to the widget's content rectangle.</param>
        /// <param name="y">The row, relative to the widget's content rectangle.</param>
        /// <returns>The tooltip text (may contain newlines), or null.</returns>
        string? GetTooltip(int x, int y);
    }
}

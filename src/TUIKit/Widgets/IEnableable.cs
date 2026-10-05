namespace TUIKit.Widgets
{
    /// <summary>
    /// Implemented by widgets that can be disabled. A disabled widget renders in a dimmed style, ignores
    /// keyboard and mouse input (its <c>HandleKey</c> and <c>HandleMouse</c> return <c>false</c>), and is
    /// skipped by focus traversal in <see cref="FocusManager"/>, <see cref="FocusScope"/>, and
    /// <see cref="Form"/>. Widgets are enabled by default.
    /// </summary>
    public interface IEnableable
    {
        /// <summary>
        /// Gets or sets a value indicating whether the widget accepts input. Defaults to true.
        /// </summary>
        bool IsEnabled { get; set; }
    }
}

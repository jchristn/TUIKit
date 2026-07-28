namespace TUIKit.Widgets
{
    using TUIKit;

    /// <summary>
    /// The contract every widget implements, whether built-in or third-party. A widget reports its
    /// desired size for an available area, then renders into a surface that has already been sized and
    /// positioned (the arrange step is expressed by handing the widget its region surface). Keeping the
    /// interface small means third-party widgets are never second-class.
    /// </summary>
    public interface IWidget
    {
        /// <summary>
        /// Reports the size the widget would like, given the available area.
        /// </summary>
        /// <param name="available">The available size.</param>
        /// <returns>The desired size, which the host may clamp.</returns>
        Size Measure(Size available);

        /// <summary>
        /// Renders the widget into the supplied surface, which represents its arranged region.
        /// </summary>
        /// <param name="surface">The region surface. Must not be null.</param>
        void Render(ISurface surface);
    }
}

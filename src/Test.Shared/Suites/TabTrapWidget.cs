namespace Test.Shared.Suites
{
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Widgets;

    /// <summary>
    /// A focusable test widget that consumes every key, Tab included, so focus can never leave it: the
    /// defect <c>FocusAudit</c> reports as a stuck traversal.
    /// </summary>
    public sealed class TabTrapWidget : IWidget, IFocusable
    {
        /// <summary>
        /// Consumes every key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>Always true.</returns>
        public bool HandleKey(KeyEvent key)
        {
            return true;
        }

        /// <summary>
        /// Takes all available space.
        /// </summary>
        /// <param name="available">The available size.</param>
        /// <returns>The available size.</returns>
        public Size Measure(Size available)
        {
            return available;
        }

        /// <summary>
        /// Draws nothing.
        /// </summary>
        /// <param name="surface">The surface.</param>
        public void Render(ISurface surface)
        {
        }
    }
}

namespace Test.Shared
{
    using System;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Widgets;

    /// <summary>
    /// A minimal focusable field that raises change events but has no <see cref="IValueSnapshot"/>, used to test
    /// <see cref="Form"/> dirty-tracking fallbacks.
    /// </summary>
    public sealed class ToggleField : IWidget, IFocusable, IChangeNotifier
    {
        /// <summary>
        /// Raised after <see cref="Toggle"/>.
        /// </summary>
        public event EventHandler? Changed;

        /// <summary>
        /// Gets a value indicating whether the toggle is on.
        /// </summary>
        public bool On { get; private set; }

        /// <summary>
        /// Flips <see cref="On"/> and raises <see cref="Changed"/>.
        /// </summary>
        public void Toggle()
        {
            On = !On;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <inheritdoc/>
        public bool HandleKey(KeyEvent key)
        {
            return false;
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            return new Size(available.Width, 1);
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            surface.DrawText(0, 0, On ? "on" : "off", CellStyle.Default);
        }
    }
}

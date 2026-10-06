namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Widgets;

    /// <summary>
    /// A focusable test widget whose visibility, enabled state, and tab-stop status can be switched, and
    /// that records every focus notification and key it receives, so focus repair and traversal can be
    /// asserted without depending on a real widget.
    /// </summary>
    public sealed class ToggleLeaf : IWidget, IFocusable, IFocusAware, IHideable, IEnableable, IFocusStop
    {
        private readonly List<bool> _FocusCalls = new List<bool>();

        /// <summary>
        /// Initializes a new instance of the <see cref="ToggleLeaf"/> class.
        /// </summary>
        /// <param name="name">The name drawn by <see cref="Render"/>. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
        public ToggleLeaf(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        /// <summary>
        /// Gets the name drawn by <see cref="Render"/>. Never null.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the widget is visible. Defaults to true.
        /// </summary>
        public bool IsVisible { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the widget is enabled. Defaults to true.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether Tab stops on the widget. Defaults to true.
        /// </summary>
        public bool IsFocusStop { get; set; } = true;

        /// <summary>
        /// Gets a value indicating whether the widget currently shows focus (the last notification).
        /// </summary>
        public bool HasFocus { get; private set; }

        /// <summary>
        /// Gets every focus notification received, oldest first. Never null.
        /// </summary>
        public IReadOnlyList<bool> FocusCalls
        {
            get { return _FocusCalls; }
        }

        /// <summary>
        /// Gets the number of keys received.
        /// </summary>
        public int KeyCount { get; private set; }

        /// <summary>
        /// Records the key and consumes Enter only.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns><c>true</c> for Enter; otherwise <c>false</c>.</returns>
        public bool HandleKey(KeyEvent key)
        {
            KeyCount++;
            return key.Code == KeyCode.Enter;
        }

        /// <summary>
        /// Records the notification.
        /// </summary>
        /// <param name="focused"><c>true</c> when focused.</param>
        public void OnFocusChanged(bool focused)
        {
            HasFocus = focused;
            _FocusCalls.Add(focused);
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
        /// Draws the name, prefixed with <c>*</c> while focused.
        /// </summary>
        /// <param name="surface">The surface. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="surface"/> is null.</exception>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            surface.DrawText(0, 0, (HasFocus ? "*" : " ") + Name, CellStyle.Default);
        }
    }
}

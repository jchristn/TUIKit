namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Widgets;

    /// <summary>
    /// A focusable test widget that reports a fixed list of key hints (or null), so key hint resolution
    /// can be tested without depending on a real widget's hint set.
    /// </summary>
    public sealed class HintLeaf : IWidget, IFocusable, IKeyHintSource
    {
        /// <summary>
        /// Gets or sets the hints returned by <see cref="GetKeyHints"/>. Null simulates a source that
        /// returns null. Defaults to an empty list.
        /// </summary>
        public IReadOnlyList<KeyHint>? Hints { get; set; } = new List<KeyHint>();

        /// <summary>
        /// Returns <see cref="Hints"/>.
        /// </summary>
        /// <returns>The configured hints, or null.</returns>
        public IReadOnlyList<KeyHint>? GetKeyHints()
        {
            return Hints;
        }

        /// <summary>
        /// Consumes no keys.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>Always false.</returns>
        public bool HandleKey(KeyEvent key)
        {
            return false;
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

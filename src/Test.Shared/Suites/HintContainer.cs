namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Widgets;

    /// <summary>
    /// A single-child test container that reports its own key hints and exposes its child through
    /// <see cref="IFocusPathNode"/>, so tests can check that hints merge from the leaf outward.
    /// </summary>
    public sealed class HintContainer : IWidget, IFocusContainer, IFocusPathNode, IKeyHintSource
    {
        private readonly IFocusable _Child;

        /// <summary>
        /// Initializes a new instance of the <see cref="HintContainer"/> class.
        /// </summary>
        /// <param name="child">The focused child. Must not be null.</param>
        public HintContainer(IFocusable child)
        {
            _Child = child ?? throw new System.ArgumentNullException(nameof(child));
        }

        /// <summary>
        /// Gets or sets the container's own hints. Defaults to an empty list.
        /// </summary>
        public IReadOnlyList<KeyHint>? Hints { get; set; } = new List<KeyHint>();

        /// <summary>
        /// Gets the child.
        /// </summary>
        public IFocusable? FocusedChild
        {
            get { return _Child; }
        }

        /// <summary>
        /// Gets the child.
        /// </summary>
        public IFocusable? FocusedLeaf
        {
            get { return _Child; }
        }

        /// <summary>
        /// Returns <see cref="Hints"/>.
        /// </summary>
        /// <returns>The configured hints, or null.</returns>
        public IReadOnlyList<KeyHint>? GetKeyHints()
        {
            return Hints;
        }

        /// <summary>
        /// Forwards the key to the child.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>Whether the child consumed it.</returns>
        public bool HandleKey(KeyEvent key)
        {
            return _Child.HandleKey(key);
        }

        /// <summary>
        /// Never moves focus (one child).
        /// </summary>
        /// <param name="forward">The direction.</param>
        /// <returns>Always false.</returns>
        public bool MoveFocus(bool forward)
        {
            return false;
        }

        /// <summary>
        /// Does nothing (one child).
        /// </summary>
        /// <param name="first">The edge.</param>
        public void FocusEdge(bool first)
        {
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

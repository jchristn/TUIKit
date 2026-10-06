namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Widgets;

    /// <summary>
    /// A container test widget that contributes its own hints and reports hint ordering options
    /// (<see cref="IKeyHintSourceOptions"/>), so resolver ordering and exclusivity can be tested.
    /// </summary>
    public sealed class OptionsHintContainer : IWidget, IFocusPathNode, IFocusable, IKeyHintSource, IKeyHintSourceOptions
    {
        private readonly IFocusable _Child;

        /// <summary>
        /// Initializes a new instance of the <see cref="OptionsHintContainer"/> class.
        /// </summary>
        /// <param name="child">The focused child. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="child"/> is null.</exception>
        public OptionsHintContainer(IFocusable child)
        {
            _Child = child ?? throw new ArgumentNullException(nameof(child));
        }

        /// <summary>
        /// Gets or sets the container's own hints. Defaults to empty.
        /// </summary>
        public IReadOnlyList<KeyHint> Hints { get; set; } = new List<KeyHint>();

        /// <summary>
        /// Gets or sets the hint order. Defaults to <see cref="KeyHintOrder.InnerFirst"/>.
        /// </summary>
        public KeyHintOrder HintOrder { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether outer sources are skipped. Defaults to false.
        /// </summary>
        public bool Exclusive { get; set; }

        /// <summary>
        /// Gets the child.
        /// </summary>
        public IFocusable? FocusedChild
        {
            get { return _Child; }
        }

        /// <summary>
        /// Returns <see cref="Hints"/>.
        /// </summary>
        /// <returns>The hints.</returns>
        public IReadOnlyList<KeyHint>? GetKeyHints()
        {
            return Hints;
        }

        /// <summary>
        /// Forwards the key to the child.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns><c>true</c> when consumed.</returns>
        public bool HandleKey(KeyEvent key)
        {
            return _Child.HandleKey(key);
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

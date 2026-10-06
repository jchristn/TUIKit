namespace Test.Shared.Suites
{
    using System;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Widgets;

    /// <summary>
    /// A container test widget that exposes a <see cref="FocusScope"/> on the focus path (it implements
    /// <see cref="IFocusPathNode"/> and returns the scope as its focused child), the way a real container
    /// that owns a scope should, so the host's focus repair can reach the scope.
    /// </summary>
    public sealed class PathScopeWidget : IWidget, IFocusContainer, IFocusAware, IFocusPathNode
    {
        private readonly FocusScope _Scope;

        /// <summary>
        /// Initializes a new instance of the <see cref="PathScopeWidget"/> class.
        /// </summary>
        /// <param name="scope">The scope. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="scope"/> is null.</exception>
        public PathScopeWidget(FocusScope scope)
        {
            _Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        }

        /// <summary>
        /// Gets the scope, which is this widget's focused child on the focus path.
        /// </summary>
        public IFocusable? FocusedChild
        {
            get { return _Scope; }
        }

        /// <summary>
        /// Gets the scope's focused leaf.
        /// </summary>
        public IFocusable? FocusedLeaf
        {
            get { return _Scope.FocusedLeaf; }
        }

        /// <summary>
        /// Forwards the key to the scope.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns><c>true</c> when consumed.</returns>
        public bool HandleKey(KeyEvent key)
        {
            return _Scope.HandleKey(key);
        }

        /// <summary>
        /// Forwards traversal to the scope.
        /// </summary>
        /// <param name="forward"><c>true</c> for Tab.</param>
        /// <returns><c>true</c> when focus moved inside.</returns>
        public bool MoveFocus(bool forward)
        {
            return _Scope.MoveFocus(forward);
        }

        /// <summary>
        /// Focuses the first or last child.
        /// </summary>
        /// <param name="first"><c>true</c> for the first.</param>
        public void FocusEdge(bool first)
        {
            _Scope.FocusEdge(first);
        }

        /// <summary>
        /// Forwards the focus change to the scope.
        /// </summary>
        /// <param name="focused"><c>true</c> when focused.</param>
        public void OnFocusChanged(bool focused)
        {
            _Scope.OnFocusChanged(focused);
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

namespace Test.Shared.Suites
{
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Widgets;

    /// <summary>
    /// A test widget that exposes a <see cref="FocusScope"/> as a bindable focus container.
    /// </summary>
    public sealed class ScopeWidget : IWidget, IFocusContainer, IFocusAware
    {
        private readonly FocusScope _Scope;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScopeWidget"/> class.
        /// </summary>
        /// <param name="scope">The scope.</param>
        public ScopeWidget(FocusScope scope)
        {
            _Scope = scope;
        }

        /// <inheritdoc/>
        public IFocusable? FocusedLeaf
        {
            get { return _Scope.FocusedLeaf; }
        }

        /// <inheritdoc/>
        public bool HandleKey(KeyEvent key)
        {
            return _Scope.HandleKey(key);
        }

        /// <inheritdoc/>
        public bool MoveFocus(bool forward)
        {
            return _Scope.MoveFocus(forward);
        }

        /// <inheritdoc/>
        public void FocusEdge(bool first)
        {
            _Scope.FocusEdge(first);
        }

        /// <inheritdoc/>
        public void OnFocusChanged(bool focused)
        {
            _Scope.OnFocusChanged(focused);
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            return available;
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
        }
    }
}

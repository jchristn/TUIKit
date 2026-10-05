namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Widgets;

    /// <summary>
    /// A deliberately broken three-child focus container: Tab visits every child, but Shift+Tab from the
    /// last child jumps straight to the first, skipping the middle one. <c>FocusAudit</c> must report it
    /// as an asymmetric traversal.
    /// </summary>
    public sealed class SkippingScope : IWidget, IFocusContainer, IFocusPathNode
    {
        private readonly List<IFocusable> _Children = new List<IFocusable> { new HintLeaf(), new HintLeaf(), new HintLeaf() };
        private int _Index;

        /// <summary>
        /// Gets the focused child.
        /// </summary>
        public IFocusable? FocusedChild
        {
            get { return _Children[_Index]; }
        }

        /// <summary>
        /// Gets the focused child.
        /// </summary>
        public IFocusable? FocusedLeaf
        {
            get { return _Children[_Index]; }
        }

        /// <summary>
        /// Moves among the children, skipping the middle one when moving backward from the end.
        /// </summary>
        /// <param name="forward">The direction.</param>
        /// <returns>Whether focus moved inside the container.</returns>
        public bool MoveFocus(bool forward)
        {
            if (forward)
            {
                if (_Index >= _Children.Count - 1)
                    return false;

                _Index++;
                return true;
            }

            if (_Index == 0)
                return false;

            _Index = 0;
            return true;
        }

        /// <summary>
        /// Focuses the first or last child.
        /// </summary>
        /// <param name="first">True for the first child.</param>
        public void FocusEdge(bool first)
        {
            _Index = first ? 0 : _Children.Count - 1;
        }

        /// <summary>
        /// Handles Tab and Shift+Tab by moving focus.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>Whether the key was consumed.</returns>
        public bool HandleKey(KeyEvent key)
        {
            if (key.Code != KeyCode.Tab)
                return false;

            return MoveFocus((key.Modifiers & KeyModifiers.Shift) == 0);
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

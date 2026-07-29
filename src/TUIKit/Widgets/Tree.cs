namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;

    /// <summary>
    /// A hierarchical list that renders expandable nodes with indentation guides and a selection.
    /// Up/Down move the selection, Right expands or steps into a node, Left collapses or steps out,
    /// and Enter toggles. Nodes are supplied through selector delegates so any object graph works.
    /// </summary>
    /// <typeparam name="T">The node type.</typeparam>
    public sealed class Tree<T> : IWidget, IFocusable
        where T : notnull
    {
        private readonly T _Root;
        private readonly Func<T, IEnumerable<T>> _Children;
        private readonly Func<T, string> _Label;
        private readonly HashSet<T> _Expanded = new HashSet<T>();
        private readonly List<T> _VisibleNodes = new List<T>();
        private readonly List<int> _VisibleDepths = new List<int>();
        private int _Selected;
        private int _Top;

        /// <summary>
        /// Gets or sets the highlight style for the selected node. Defaults to reversed cyan.
        /// </summary>
        public CellStyle HighlightStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromRgb(0, 0, 0)).WithBackground(Color.FromPalette(6));

        /// <summary>
        /// Initializes a new instance of the <see cref="Tree{T}"/> class with the root expanded.
        /// </summary>
        /// <param name="root">The root node. Must not be null.</param>
        /// <param name="children">A selector returning a node's children. Must not be null.</param>
        /// <param name="label">A selector returning a node's display text. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public Tree(T root, Func<T, IEnumerable<T>> children, Func<T, string> label)
        {
            _Root = root ?? throw new ArgumentNullException(nameof(root));
            _Children = children ?? throw new ArgumentNullException(nameof(children));
            _Label = label ?? throw new ArgumentNullException(nameof(label));
            _Expanded.Add(root);
        }

        /// <summary>
        /// Gets the currently selected node, or the root when the visible list is empty.
        /// </summary>
        public T SelectedNode
        {
            get
            {
                Rebuild();
                if (_Selected >= 0 && _Selected < _VisibleNodes.Count)
                    return _VisibleNodes[_Selected];

                return _Root;
            }
        }

        /// <summary>
        /// Expands a node so its children are shown.
        /// </summary>
        /// <param name="node">The node. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="node"/> is null.</exception>
        public void Expand(T node)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));

            _Expanded.Add(node);
        }

        /// <summary>
        /// Collapses a node so its children are hidden.
        /// </summary>
        /// <param name="node">The node. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="node"/> is null.</exception>
        public void Collapse(T node)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));

            _Expanded.Remove(node);
        }

        /// <summary>
        /// Determines whether a node is expanded.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <returns><c>true</c> when the node is expanded; otherwise <c>false</c>.</returns>
        public bool IsExpanded(T node)
        {
            return node != null && _Expanded.Contains(node);
        }

        /// <summary>
        /// Handles navigation and expand/collapse keys.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns><c>true</c> when the key was consumed; otherwise <c>false</c>.</returns>
        public bool HandleKey(KeyEvent key)
        {
            Rebuild();
            switch (key.Code)
            {
                case KeyCode.Up:
                    _Selected = Math.Max(0, _Selected - 1);
                    return true;
                case KeyCode.Down:
                    _Selected = Math.Min(_VisibleNodes.Count - 1, _Selected + 1);
                    return true;
                case KeyCode.Right:
                case KeyCode.Enter:
                    if (_Selected >= 0 && _Selected < _VisibleNodes.Count)
                        _Expanded.Add(_VisibleNodes[_Selected]);
                    return true;
                case KeyCode.Left:
                    if (_Selected >= 0 && _Selected < _VisibleNodes.Count)
                        _Expanded.Remove(_VisibleNodes[_Selected]);
                    return true;
                default:
                    return false;
            }
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            Rebuild();
            return new Size(available.Width, Math.Min(available.Height, _VisibleNodes.Count));
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            Rebuild();
            int width = surface.Size.Width;
            int height = surface.Size.Height;
            if (width <= 0 || height <= 0)
                return;

            if (_Selected < _Top)
                _Top = _Selected;
            else if (_Selected >= _Top + height)
                _Top = _Selected - height + 1;

            for (int row = 0; row < height && _Top + row < _VisibleNodes.Count; row++)
            {
                int index = _Top + row;
                T node = _VisibleNodes[index];
                int depth = _VisibleDepths[index];
                bool hasChildren = HasChildren(node);
                string disclosure = hasChildren ? (_Expanded.Contains(node) ? "▾ " : "▸ ") : "  ";
                string line = new string(' ', depth * 2) + disclosure + _Label(node);

                bool selected = index == _Selected;
                CellStyle style = selected ? HighlightStyle : CellStyle.Default;
                if (selected)
                    surface.Fill(new Rect(0, row, width, 1), Cell.Blank(style));

                surface.DrawText(0, row, line, style);
            }
        }

        private void Rebuild()
        {
            _VisibleNodes.Clear();
            _VisibleDepths.Clear();
            Walk(_Root, 0);
            if (_Selected >= _VisibleNodes.Count)
                _Selected = Math.Max(0, _VisibleNodes.Count - 1);
        }

        private void Walk(T node, int depth)
        {
            _VisibleNodes.Add(node);
            _VisibleDepths.Add(depth);
            if (!_Expanded.Contains(node))
                return;

            foreach (T child in _Children(node))
                Walk(child, depth + 1);
        }

        private bool HasChildren(T node)
        {
            foreach (T unused in _Children(node))
                return true;

            return false;
        }
    }
}

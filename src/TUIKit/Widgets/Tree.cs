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
    /// <remarks>
    /// The children selector is invoked at most once per node and the result is cached until
    /// <see cref="Refresh"/> or <see cref="Invalidate"/> is called, so no user delegate runs work
    /// proportional to the tree on every render. The disclosure glyph uses the optional
    /// <c>hasChildren</c> probe (an O(1) "can expand?" test such as "is a directory") and never
    /// enumerates children; when the probe is null the children selector is consulted once and the
    /// boolean cached. All keyed state (expansion, caches) goes through the supplied comparer, so
    /// regenerated nodes that compare equal keep their expansion. Not thread-safe.
    /// </remarks>
    /// <typeparam name="T">The node type.</typeparam>
    public sealed class Tree<T> : IWidget, IFocusable, IMouseAware
        where T : notnull
    {
        private readonly T _Root;
        private readonly Func<T, IEnumerable<T>> _Children;
        private readonly Func<T, string> _Label;
        private readonly Func<T, bool>? _HasChildren;
        private readonly IEqualityComparer<T> _Comparer;
        private readonly HashSet<T> _Expanded;
        private readonly Dictionary<T, List<T>> _ChildCache;
        private readonly Dictionary<T, bool> _HasChildrenCache;
        private readonly List<T> _VisibleNodes = new List<T>();
        private readonly List<int> _VisibleDepths = new List<int>();
        private int _Selected;
        private int _Top;
        private int _LastViewportHeight = 1;
        private int _HoverIndex = -1;

        /// <summary>
        /// Gets or sets the highlight style for the selected node. Defaults to reversed cyan.
        /// </summary>
        public CellStyle HighlightStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromRgb(0, 0, 0)).WithBackground(Color.FromPalette(6));

        /// <summary>
        /// Gets or sets the style of the non-selected visible node under the pointer while hover
        /// tracking is on. The selected node keeps <see cref="HighlightStyle"/> when hovered.
        /// Defaults to underlined default text.
        /// </summary>
        public CellStyle HoverStyle { get; set; } = CellStyle.Default.WithAttributes(CellAttributes.Underline);

        /// <summary>
        /// Initializes a new instance of the <see cref="Tree{T}"/> class with the root expanded.
        /// </summary>
        /// <param name="root">The root node. Must not be null.</param>
        /// <param name="children">A selector returning a node's children. Invoked at most once per node and cached. Must not be null.</param>
        /// <param name="label">A selector returning a node's display text. Must not be null.</param>
        /// <param name="hasChildren">
        /// An optional cheap "can expand?" test used for the disclosure glyph, avoiding child enumeration
        /// on every render. When null, the children selector is probed once per node and the boolean cached.
        /// </param>
        /// <param name="comparer">Equality used for all keyed state. Defaults to <see cref="EqualityComparer{T}.Default"/> when null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="root"/>, <paramref name="children"/>, or <paramref name="label"/> is null.</exception>
        public Tree(
            T root,
            Func<T, IEnumerable<T>> children,
            Func<T, string> label,
            Func<T, bool>? hasChildren = null,
            IEqualityComparer<T>? comparer = null)
        {
            _Root = root ?? throw new ArgumentNullException(nameof(root));
            _Children = children ?? throw new ArgumentNullException(nameof(children));
            _Label = label ?? throw new ArgumentNullException(nameof(label));
            _HasChildren = hasChildren;
            _Comparer = comparer ?? EqualityComparer<T>.Default;
            _Expanded = new HashSet<T>(_Comparer);
            _ChildCache = new Dictionary<T, List<T>>(_Comparer);
            _HasChildrenCache = new Dictionary<T, bool>(_Comparer);
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
        /// Drops the cached children of a single node so the children selector is re-invoked on the next
        /// build. Use it when a node's children change (for example a directory gains files).
        /// </summary>
        /// <param name="node">The node whose cache to drop. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="node"/> is null.</exception>
        public void Invalidate(T node)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));

            _ChildCache.Remove(node);
            _HasChildrenCache.Remove(node);
        }

        /// <summary>
        /// Drops every cached child set so the children selector is re-invoked for all nodes on the next
        /// build. Expansion state is preserved.
        /// </summary>
        public void Refresh()
        {
            _ChildCache.Clear();
            _HasChildrenCache.Clear();
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
                case KeyCode.PageUp:
                    _Selected = Math.Max(0, _Selected - Math.Max(1, _LastViewportHeight));
                    return true;
                case KeyCode.PageDown:
                    _Selected = Math.Min(_VisibleNodes.Count - 1, _Selected + Math.Max(1, _LastViewportHeight));
                    return true;
                case KeyCode.Home:
                    _Selected = 0;
                    return true;
                case KeyCode.End:
                    _Selected = Math.Max(0, _VisibleNodes.Count - 1);
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

        /// <summary>
        /// Selects the visible node under a left press (a second press on the already-selected node
        /// toggles its expansion), steps the selection with the wheel, and tracks the hovered node for
        /// <see cref="HoverStyle"/> rendering. Enter/Move/Leave events are observed but never
        /// consumed.
        /// </summary>
        /// <param name="mouse">The mouse event in widget-local coordinates. Must not be null.</param>
        /// <returns><c>true</c> when a press or wheel changed selection or expansion; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mouse"/> is null.</exception>
        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            Rebuild();
            switch (mouse.Kind)
            {
                case MouseEventKind.Press:
                    if (mouse.Button == MouseButton.Left)
                    {
                        int pressed = NodeIndexAt(mouse.Y);
                        if (pressed >= 0)
                        {
                            if (pressed == _Selected)
                            {
                                T node = _VisibleNodes[pressed];
                                if (!_Expanded.Remove(node))
                                    _Expanded.Add(node);
                            }
                            else
                            {
                                _Selected = pressed;
                            }

                            return true;
                        }
                    }

                    return false;
                case MouseEventKind.Wheel:
                    if (mouse.Button == MouseButton.WheelUp)
                    {
                        _Selected = Math.Max(0, _Selected - 1);
                        return true;
                    }

                    if (mouse.Button == MouseButton.WheelDown)
                    {
                        _Selected = Math.Min(_VisibleNodes.Count - 1, _Selected + 1);
                        return true;
                    }

                    return false;
                case MouseEventKind.Enter:
                case MouseEventKind.Move:
                    _HoverIndex = NodeIndexAt(mouse.Y);
                    return false;
                case MouseEventKind.Leave:
                    _HoverIndex = -1;
                    return false;
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

            _LastViewportHeight = height;

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
                CellStyle style = selected ? HighlightStyle : (index == _HoverIndex ? HoverStyle : CellStyle.Default);
                if (selected)
                    surface.Fill(new Rect(0, row, width, 1), Cell.Blank(style));

                surface.DrawText(0, row, line, style);
            }
        }

        private int NodeIndexAt(int y)
        {
            if (y < 0)
                return -1;

            int index = _Top + y;
            return index < _VisibleNodes.Count ? index : -1;
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

            List<T> children = GetChildren(node);
            for (int i = 0; i < children.Count; i++)
                Walk(children[i], depth + 1);
        }

        private List<T> GetChildren(T node)
        {
            if (_ChildCache.TryGetValue(node, out List<T>? cached))
                return cached;

            List<T> resolved = new List<T>();
            IEnumerable<T> source = _Children(node);
            if (source != null)
            {
                foreach (T child in source)
                    resolved.Add(child);
            }

            _ChildCache[node] = resolved;
            return resolved;
        }

        private bool HasChildren(T node)
        {
            if (_HasChildren != null)
                return _HasChildren(node);

            if (_HasChildrenCache.TryGetValue(node, out bool cached))
                return cached;

            bool result = GetChildren(node).Count > 0;
            _HasChildrenCache[node] = result;
            return result;
        }
    }
}

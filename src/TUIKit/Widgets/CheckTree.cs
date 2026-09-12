namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using TUIKit;
    using TUIKit.Input;

    /// <summary>
    /// A forest of expandable nodes with cascading tri-state checkboxes. Checking a node makes its
    /// subtree effectively checked; unchecking a descendant carves a hole and marks its ancestors
    /// <see cref="CheckState.Partial"/>. A node's effective state is inherited from its nearest explicit
    /// ancestor, defaulting to unchecked at a root. Children are loaded lazily and cached, so no user
    /// delegate runs work proportional to the tree on every render. State is keyed through the supplied
    /// comparer, so regenerated nodes that compare equal keep their expansion and checks. Space toggles
    /// the selected node; Enter is deliberately not consumed so a host can bind it to confirm. Not
    /// thread-safe; drive it from the UI loop.
    /// </summary>
    /// <typeparam name="T">The node type.</typeparam>
    public sealed class CheckTree<T> : IWidget, IFocusable, IFocusAware, IMouseAware
        where T : notnull
    {
        private readonly List<T> _Roots = new List<T>();
        private readonly Func<T, IEnumerable<T>> _Children;
        private readonly Func<T, string> _Label;
        private readonly Func<T, bool>? _HasChildren;
        private readonly IEqualityComparer<T> _Comparer;
        private readonly HashSet<T> _Expanded;
        private readonly Dictionary<T, bool> _Explicit;
        private readonly Dictionary<T, List<T>> _ChildCache;
        private readonly Dictionary<T, bool> _HasChildrenCache;
        private readonly Dictionary<T, T> _Parent;
        private readonly List<T> _VisibleNodes = new List<T>();
        private readonly List<int> _VisibleDepths = new List<int>();
        private int _Selected;
        private int _Top;
        private int _LastViewportHeight = 1;
        private bool _Focused = true;
        private string _CheckedGlyph = "[x] ";
        private string _UncheckedGlyph = "[ ] ";
        private string _PartialGlyph = "[~] ";

        /// <summary>
        /// Initializes a new instance of the <see cref="CheckTree{T}"/> class.
        /// </summary>
        /// <param name="roots">The forest roots (for example drive roots). Must not be null or empty.</param>
        /// <param name="children">Lazy child provider; invoked at most once per node and cached. Must not be null.</param>
        /// <param name="label">Renders a node's text. Must not be null.</param>
        /// <param name="hasChildren">
        /// A cheap "can expand?" test (for example "is a directory") avoiding enumeration; when null, a
        /// single cached probe of <paramref name="children"/> is used.
        /// </param>
        /// <param name="comparer">Equality for keyed state. Defaults to <see cref="EqualityComparer{T}.Default"/> when null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="roots"/>, <paramref name="children"/>, or <paramref name="label"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="roots"/> is empty.</exception>
        public CheckTree(
            IReadOnlyList<T> roots,
            Func<T, IEnumerable<T>> children,
            Func<T, string> label,
            Func<T, bool>? hasChildren = null,
            IEqualityComparer<T>? comparer = null)
        {
            if (roots == null)
                throw new ArgumentNullException(nameof(roots));
            if (roots.Count == 0)
                throw new ArgumentException("At least one root is required.", nameof(roots));

            _Children = children ?? throw new ArgumentNullException(nameof(children));
            _Label = label ?? throw new ArgumentNullException(nameof(label));
            _HasChildren = hasChildren;
            _Comparer = comparer ?? EqualityComparer<T>.Default;
            _Expanded = new HashSet<T>(_Comparer);
            _Explicit = new Dictionary<T, bool>(_Comparer);
            _ChildCache = new Dictionary<T, List<T>>(_Comparer);
            _HasChildrenCache = new Dictionary<T, bool>(_Comparer);
            _Parent = new Dictionary<T, T>(_Comparer);

            for (int i = 0; i < roots.Count; i++)
                _Roots.Add(roots[i]);
        }

        /// <summary>
        /// Raised the first time a node's children are loaded. The argument is the loaded node.
        /// </summary>
        public event Action<T>? Expanded;

        /// <summary>
        /// Raised after any check state changes through <see cref="SetExplicit"/> or <see cref="ToggleAt"/>.
        /// </summary>
        public event Action? CheckChanged;

        /// <summary>
        /// Raised after the selection moves.
        /// </summary>
        public event Action? SelectionChanged;

        /// <summary>
        /// Gets or sets the glyph drawn for a checked node. Defaults to "[x] ". Must not be null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string CheckedGlyph
        {
            get { return _CheckedGlyph; }
            set { _CheckedGlyph = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>
        /// Gets or sets the glyph drawn for an unchecked node. Defaults to "[ ] ". Must not be null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string UncheckedGlyph
        {
            get { return _UncheckedGlyph; }
            set { _UncheckedGlyph = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>
        /// Gets or sets the glyph drawn for a partially-checked node. Defaults to "[~] ". Must not be null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public string PartialGlyph
        {
            get { return _PartialGlyph; }
            set { _PartialGlyph = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>
        /// Gets or sets a per-node style selector, or null to draw every row with the default style.
        /// </summary>
        public Func<T, CellStyle>? RowStyle { get; set; }

        /// <summary>
        /// Gets or sets the highlight style for the selected node when focused. Defaults to black on cyan.
        /// </summary>
        public CellStyle HighlightStyle { get; set; } = CellStyle.Default
            .WithForeground(Color.FromRgb(0, 0, 0))
            .WithBackground(Color.FromPalette(6));

        /// <summary>
        /// Gets or sets the base style applied to unselected rows and the surface fill when
        /// <see cref="RowStyle"/> is null (when <see cref="RowStyle"/> is set it wins per node).
        /// Defaults to <see cref="CellStyle.Default"/>; assign a style with a background to give the
        /// tree a solid background.
        /// </summary>
        public CellStyle NormalStyle { get; set; } = CellStyle.Default;

        /// <summary>
        /// Gets the currently selected node, or the first root when the visible list is empty.
        /// </summary>
        public T SelectedNode
        {
            get
            {
                Rebuild();
                if (_Selected >= 0 && _Selected < _VisibleNodes.Count)
                    return _VisibleNodes[_Selected];

                return _Roots[0];
            }
        }

        /// <summary>
        /// Sets, or clears, a node's explicit check override.
        /// </summary>
        /// <param name="node">The node. Must not be null.</param>
        /// <param name="state"><c>true</c> or <c>false</c> to override; <c>null</c> to inherit from the nearest explicit ancestor.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="node"/> is null.</exception>
        public void SetExplicit(T node, bool? state)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));

            if (state.HasValue)
                _Explicit[node] = state.Value;
            else
                _Explicit.Remove(node);

            CheckChanged?.Invoke();
        }

        /// <summary>
        /// Reports a node's effective checked state, inherited from its nearest explicit ancestor and
        /// defaulting to unchecked at a root.
        /// </summary>
        /// <param name="node">The node. Must not be null.</param>
        /// <returns><c>true</c> when the node is effectively checked; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="node"/> is null.</exception>
        public bool EffectiveChecked(T node)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));

            T current = node;
            while (true)
            {
                if (_Explicit.TryGetValue(current, out bool value))
                    return value;

                if (!_Parent.TryGetValue(current, out T? parent))
                    return false;

                current = parent;
            }
        }

        /// <summary>
        /// Reports a node's tri-state status for display. Only loaded descendants are inspected, so an
        /// unopened branch never forces <see cref="CheckState.Partial"/>.
        /// </summary>
        /// <param name="node">The node. Must not be null.</param>
        /// <returns>The node's <see cref="CheckState"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="node"/> is null.</exception>
        public CheckState State(T node)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));

            bool effective = EffectiveChecked(node);
            if (LoadedDescendantDiffers(node, effective))
                return CheckState.Partial;

            return effective ? CheckState.Checked : CheckState.Unchecked;
        }

        /// <summary>
        /// Toggles a node's effective state, clears explicit overrides on its loaded descendants so the
        /// subtree inherits cleanly, and auto-expands one level when the node becomes checked.
        /// </summary>
        /// <param name="node">The node. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="node"/> is null.</exception>
        public void ToggleAt(T node)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));

            bool newState = !EffectiveChecked(node);
            _Explicit[node] = newState;
            ClearLoadedDescendantOverrides(node);

            if (newState && HasChildren(node))
                Expand(node);

            CheckChanged?.Invoke();
        }

        /// <summary>
        /// Returns the top-most effectively-checked nodes: the root of each checked subtree, including a
        /// re-checked node beneath a hole. Never null.
        /// </summary>
        /// <returns>The included roots, in depth-first order.</returns>
        public IReadOnlyList<T> IncludedRoots()
        {
            List<T> result = new List<T>();
            for (int i = 0; i < _Roots.Count; i++)
                CollectIncludes(_Roots[i], false, result);

            return result;
        }

        /// <summary>
        /// Returns the top-most unchecked nodes that sit beneath a checked ancestor — the holes carved out
        /// of an included subtree. Never null.
        /// </summary>
        /// <returns>The excluded holes, in depth-first order.</returns>
        public IReadOnlyList<T> ExcludedHoles()
        {
            List<T> result = new List<T>();
            for (int i = 0; i < _Roots.Count; i++)
                CollectHoles(_Roots[i], false, result);

            return result;
        }

        /// <summary>
        /// Expands a node, loading its children on first expansion.
        /// </summary>
        /// <param name="node">The node. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="node"/> is null.</exception>
        public void Expand(T node)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));

            GetChildren(node);
            _Expanded.Add(node);
        }

        /// <summary>
        /// Collapses a node so its children are hidden. Loaded state and checks are retained.
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
        /// <returns><c>true</c> when expanded; otherwise <c>false</c>.</returns>
        public bool IsExpanded(T node)
        {
            return node != null && _Expanded.Contains(node);
        }

        /// <summary>
        /// Loads and expands the ancestor chain down to a node, then selects it. Does nothing when the
        /// node cannot be reached from any root. Intended for pre-seeding a saved selection.
        /// </summary>
        /// <param name="node">The node to reveal. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="node"/> is null.</exception>
        public void RevealTo(T node)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));

            List<T> path = new List<T>();
            HashSet<T> visited = new HashSet<T>(_Comparer);
            for (int i = 0; i < _Roots.Count; i++)
            {
                if (FindPath(_Roots[i], node, path, visited))
                {
                    for (int p = 0; p < path.Count - 1; p++)
                        Expand(path[p]);

                    Rebuild();
                    int index = IndexOfVisible(node);
                    if (index >= 0)
                    {
                        _Selected = index;
                        SelectionChanged?.Invoke();
                    }

                    return;
                }

                path.Clear();
            }
        }

        /// <inheritdoc/>
        public void OnFocusChanged(bool focused)
        {
            _Focused = focused;
        }

        /// <summary>
        /// Handles navigation and toggling. Space toggles the selected node; Enter is not consumed so the
        /// host can bind it to confirm.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns><c>true</c> when the key was consumed; otherwise <c>false</c>.</returns>
        public bool HandleKey(KeyEvent key)
        {
            Rebuild();
            switch (key.Code)
            {
                case KeyCode.Up:
                    Move(Math.Max(0, _Selected - 1));
                    return true;
                case KeyCode.Down:
                    Move(Math.Min(_VisibleNodes.Count - 1, _Selected + 1));
                    return true;
                case KeyCode.PageUp:
                    Move(Math.Max(0, _Selected - Math.Max(1, _LastViewportHeight)));
                    return true;
                case KeyCode.PageDown:
                    Move(Math.Min(_VisibleNodes.Count - 1, _Selected + Math.Max(1, _LastViewportHeight)));
                    return true;
                case KeyCode.Home:
                    Move(0);
                    return true;
                case KeyCode.End:
                    Move(Math.Max(0, _VisibleNodes.Count - 1));
                    return true;
                case KeyCode.Right:
                    if (_Selected >= 0 && _Selected < _VisibleNodes.Count)
                        Expand(_VisibleNodes[_Selected]);
                    return true;
                case KeyCode.Left:
                    if (_Selected >= 0 && _Selected < _VisibleNodes.Count)
                        Collapse(_VisibleNodes[_Selected]);
                    return true;
                case KeyCode.Character:
                    if (key.Rune == ' ')
                    {
                        if (_Selected >= 0 && _Selected < _VisibleNodes.Count)
                            ToggleAt(_VisibleNodes[_Selected]);
                        return true;
                    }

                    return false;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Selects the row under the pointer on a left press (and toggles it where the widget is a check
        /// widget), and moves the selection one row per wheel notch. Coordinates are widget-local.
        /// </summary>
        /// <param name="mouse">The mouse event in widget-local coordinates. Must not be null.</param>
        /// <returns><c>true</c> when the event changed the selection; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mouse"/> is null.</exception>
        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            int count = _VisibleNodes.Count;
            if (count == 0)
                return false;

            if (mouse.Kind == MouseEventKind.Wheel)
            {
                if (mouse.Button == MouseButton.WheelUp)
                {
                    _Selected = Math.Max(0, _Selected - 1);
                    return true;
                }

                if (mouse.Button == MouseButton.WheelDown)
                {
                    _Selected = Math.Min(count - 1, _Selected + 1);
                    return true;
                }

                return false;
            }

            if (mouse.Kind == MouseEventKind.Press && mouse.Button == MouseButton.Left)
            {
                int index = _Top + (mouse.Y);
                if (index >= 0 && index < count)
                {
                    _Selected = index;
                    ToggleAt(_VisibleNodes[index]);
                    return true;
                }
            }

            return false;
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

            if (NormalStyle.Background.Kind != ColorKind.Default)
                surface.Fill(new Rect(0, 0, width, height), Cell.Blank(NormalStyle));

            for (int row = 0; row < height && _Top + row < _VisibleNodes.Count; row++)
            {
                int index = _Top + row;
                T node = _VisibleNodes[index];
                int depth = _VisibleDepths[index];
                bool expandable = HasChildren(node);
                string disclosure = expandable ? (_Expanded.Contains(node) ? "▾ " : "▸ ") : "  ";
                string glyph = GlyphFor(State(node));
                string line = new string(' ', depth * 2) + disclosure + glyph + _Label(node);

                bool selected = index == _Selected;
                CellStyle style;
                if (selected && _Focused)
                {
                    style = HighlightStyle;
                    surface.Fill(new Rect(0, row, width, 1), Cell.Blank(style));
                }
                else
                {
                    style = RowStyle != null ? RowStyle(node) : NormalStyle;
                    if (selected)
                        style = style.WithAttribute(CellAttributes.Bold, true);
                }

                surface.DrawText(0, row, line, style);
            }
        }

        private void Move(int index)
        {
            if (index == _Selected)
                return;

            _Selected = index;
            SelectionChanged?.Invoke();
        }

        private string GlyphFor(CheckState state)
        {
            switch (state)
            {
                case CheckState.Checked:
                    return _CheckedGlyph;
                case CheckState.Partial:
                    return _PartialGlyph;
                default:
                    return _UncheckedGlyph;
            }
        }

        private bool LoadedDescendantDiffers(T node, bool effective)
        {
            if (!_ChildCache.TryGetValue(node, out List<T>? children))
                return false;

            for (int i = 0; i < children.Count; i++)
            {
                T child = children[i];
                if (EffectiveChecked(child) != effective)
                    return true;

                if (LoadedDescendantDiffers(child, effective))
                    return true;
            }

            return false;
        }

        private void ClearLoadedDescendantOverrides(T node)
        {
            if (!_ChildCache.TryGetValue(node, out List<T>? children))
                return;

            for (int i = 0; i < children.Count; i++)
            {
                T child = children[i];
                _Explicit.Remove(child);
                ClearLoadedDescendantOverrides(child);
            }
        }

        private void CollectIncludes(T node, bool parentChecked, List<T> result)
        {
            bool effective = EffectiveChecked(node);
            if (effective && !parentChecked)
                result.Add(node);

            if (_ChildCache.TryGetValue(node, out List<T>? children))
            {
                for (int i = 0; i < children.Count; i++)
                    CollectIncludes(children[i], effective, result);
            }
        }

        private void CollectHoles(T node, bool parentChecked, List<T> result)
        {
            bool effective = EffectiveChecked(node);
            if (!effective && parentChecked)
                result.Add(node);

            if (_ChildCache.TryGetValue(node, out List<T>? children))
            {
                for (int i = 0; i < children.Count; i++)
                    CollectHoles(children[i], effective, result);
            }
        }

        private bool FindPath(T node, T target, List<T> path, HashSet<T> visited)
        {
            if (!visited.Add(node))
                return false;

            path.Add(node);
            if (_Comparer.Equals(node, target))
                return true;

            List<T> children = GetChildren(node);
            for (int i = 0; i < children.Count; i++)
            {
                if (FindPath(children[i], target, path, visited))
                    return true;
            }

            path.RemoveAt(path.Count - 1);
            return false;
        }

        private int IndexOfVisible(T node)
        {
            for (int i = 0; i < _VisibleNodes.Count; i++)
            {
                if (_Comparer.Equals(_VisibleNodes[i], node))
                    return i;
            }

            return -1;
        }

        private void Rebuild()
        {
            _VisibleNodes.Clear();
            _VisibleDepths.Clear();
            for (int i = 0; i < _Roots.Count; i++)
                Walk(_Roots[i], 0);

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
            try
            {
                IEnumerable<T> source = _Children(node);
                if (source != null)
                {
                    foreach (T child in source)
                    {
                        resolved.Add(child);
                        _Parent[child] = node;
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                resolved.Clear();
            }
            catch (IOException)
            {
                resolved.Clear();
            }

            _ChildCache[node] = resolved;
            Expanded?.Invoke(node);
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

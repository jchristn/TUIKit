namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// An immutable snapshot of where keyboard focus is: the focused region and the chain of widgets
    /// from that region's bound widget down to the focused leaf. The host publishes the current path as
    /// <c>TuiApplication.CurrentFocusPath</c> and raises <c>FocusPathChanged</c> whenever it changes,
    /// whether focus moved between regions or inside a container. Use it to render "focus is within this
    /// pane", to resolve key hints from the leaf outward (<see cref="Input.KeyHintResolver"/>), or to show
    /// a breadcrumb.
    /// </summary>
    /// <remarks>
    /// Nodes are compared by reference. A path is built by following
    /// <see cref="IFocusPathNode.FocusedChild"/>; a container that implements only
    /// <see cref="IFocusContainer"/> contributes its <see cref="IFocusContainer.FocusedLeaf"/> as the
    /// final node. Instances are immutable and safe to share across threads.
    /// </remarks>
    public sealed class FocusPath : IEquatable<FocusPath>
    {
        private const int MaxDepth = 64;

        private static readonly FocusPath _Empty = new FocusPath(null, new List<object>());

        private readonly List<object> _Nodes;

        /// <summary>
        /// Gets the empty path: no region holds focus. Never null.
        /// </summary>
        public static FocusPath Empty
        {
            get { return _Empty; }
        }

        /// <summary>
        /// Gets the id of the focused region, or null when no region holds focus.
        /// </summary>
        public string? Region { get; }

        /// <summary>
        /// Gets the nodes from the region's bound widget (index 0) down to the focused leaf (last).
        /// Empty when no region holds focus. Never null.
        /// </summary>
        public IReadOnlyList<object> Nodes
        {
            get { return _Nodes; }
        }

        /// <summary>
        /// Gets the focused leaf: the last node, when it is focusable; otherwise null.
        /// </summary>
        public IFocusable? Leaf
        {
            get { return _Nodes.Count == 0 ? null : _Nodes[_Nodes.Count - 1] as IFocusable; }
        }

        /// <summary>
        /// Gets the number of nodes in the path. Zero when empty.
        /// </summary>
        public int Depth
        {
            get { return _Nodes.Count; }
        }

        /// <summary>
        /// Gets a value indicating whether no region holds focus.
        /// </summary>
        public bool IsEmpty
        {
            get { return _Nodes.Count == 0; }
        }

        internal FocusPath(string? region, List<object> nodes)
        {
            Region = region;
            _Nodes = nodes;
        }

        /// <summary>
        /// Builds the path for a region whose bound widget is <paramref name="root"/>, following
        /// <see cref="IFocusPathNode.FocusedChild"/> down to the leaf. Applications hosted by
        /// <c>TuiApplication</c> read <c>CurrentFocusPath</c> instead; this is for custom hosts and tests.
        /// </summary>
        /// <param name="region">The region id, or null.</param>
        /// <param name="root">The region's bound widget, or null for an empty path.</param>
        /// <returns>The path. Never null; <see cref="Empty"/> when <paramref name="root"/> is null.</returns>
        public static FocusPath Build(string? region, object? root)
        {
            if (root == null)
                return _Empty;

            List<object> nodes = new List<object>();
            Collect(root, nodes);
            return new FocusPath(region, nodes);
        }

        /// <summary>
        /// Determines whether a widget lies on the path (by reference): the focused leaf or any container
        /// that holds it.
        /// </summary>
        /// <param name="widget">The widget. Must not be null.</param>
        /// <returns><c>true</c> when the widget is on the path; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="widget"/> is null.</exception>
        public bool Contains(object widget)
        {
            if (widget == null)
                throw new ArgumentNullException(nameof(widget));

            for (int i = 0; i < _Nodes.Count; i++)
            {
                if (ReferenceEquals(_Nodes[i], widget))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether another path names the same region and the same nodes, by reference.
        /// </summary>
        /// <param name="other">The other path, or null.</param>
        /// <returns><c>true</c> when equal; otherwise <c>false</c>.</returns>
        public bool Equals(FocusPath? other)
        {
            if (other == null)
                return false;
            if (ReferenceEquals(this, other))
                return true;

            return string.Equals(Region, other.Region, StringComparison.Ordinal) && SameNodes(other._Nodes);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return Equals(obj as FocusPath);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Region != null ? StringComparer.Ordinal.GetHashCode(Region) : 0;
                for (int i = 0; i < _Nodes.Count; i++)
                    hash = (hash * 31) ^ System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(_Nodes[i]);

                return hash;
            }
        }

        /// <summary>
        /// Returns the region and the node type names, for diagnostics: <c>main: TabView &gt; Form &gt; TextField</c>.
        /// </summary>
        /// <returns>A readable description. Never null.</returns>
        public override string ToString()
        {
            if (_Nodes.Count == 0)
                return "(no focus)";

            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            builder.Append(Region ?? "?").Append(": ");
            for (int i = 0; i < _Nodes.Count; i++)
            {
                if (i > 0)
                    builder.Append(" > ");
                builder.Append(TypeName(_Nodes[i].GetType()));
            }

            return builder.ToString();
        }

        internal static void Collect(object root, List<object> nodes)
        {
            object? current = root;
            while (current != null && nodes.Count < MaxDepth)
            {
                nodes.Add(current);
                if (current is IFocusPathNode node)
                {
                    IFocusable? child = node.FocusedChild;
                    if (child == null || ReferenceEquals(child, current))
                        return;

                    current = child;
                    continue;
                }

                if (current is IFocusContainer container)
                {
                    IFocusable? leaf = container.FocusedLeaf;
                    if (leaf != null && !ReferenceEquals(leaf, current))
                        nodes.Add(leaf);
                }

                return;
            }
        }

        internal bool SameAs(string? region, List<object> nodes)
        {
            return string.Equals(Region, region, StringComparison.Ordinal) && SameNodes(nodes);
        }

        private bool SameNodes(List<object> nodes)
        {
            if (nodes.Count != _Nodes.Count)
                return false;

            for (int i = 0; i < nodes.Count; i++)
            {
                if (!ReferenceEquals(nodes[i], _Nodes[i]))
                    return false;
            }

            return true;
        }

        private static string TypeName(Type type)
        {
            string name = type.Name;
            int tick = name.IndexOf('`');
            return tick > 0 ? name.Substring(0, tick) : name;
        }
    }
}

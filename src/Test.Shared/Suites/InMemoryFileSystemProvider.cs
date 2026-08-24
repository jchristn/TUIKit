namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using TUIKit.Widgets;

    /// <summary>
    /// A deterministic, disk-free <see cref="IFileSystemProvider"/> backed by an in-memory tree, used by
    /// the file-selection suites so they run identically on any OS. Directories are the keys of the
    /// child map (a value of an empty array means an empty directory); every other listed child is a
    /// file. Hidden entries are those whose last path segment begins with a dot. Path comparison is
    /// ordinal. It records per-node child-provider call counts so a suite can assert the lazy-cache
    /// contract. This type is test-only and never shipped.
    /// </summary>
    public sealed class InMemoryFileSystemProvider : IFileSystemProvider
    {
        private readonly List<string> _Roots = new List<string>();
        private readonly Dictionary<string, string[]> _Children;
        private readonly Dictionary<string, int> _Calls = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// Initializes a new instance of the <see cref="InMemoryFileSystemProvider"/> class.
        /// </summary>
        /// <param name="roots">The forest roots. Must not be null.</param>
        /// <param name="children">A map from a directory path to its child paths. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public InMemoryFileSystemProvider(IReadOnlyList<string> roots, Dictionary<string, string[]> children)
        {
            if (roots == null)
                throw new ArgumentNullException(nameof(roots));

            _Children = children ?? throw new ArgumentNullException(nameof(children));
            for (int i = 0; i < roots.Count; i++)
                _Roots.Add(roots[i]);
        }

        /// <summary>
        /// Gets the number of times the child provider was invoked for a node.
        /// </summary>
        /// <param name="path">The node path.</param>
        /// <returns>The call count, or zero when never invoked.</returns>
        public int CallCountFor(string path)
        {
            return _Calls.TryGetValue(path, out int count) ? count : 0;
        }

        /// <inheritdoc/>
        public IEqualityComparer<string> PathComparer
        {
            get { return StringComparer.Ordinal; }
        }

        /// <inheritdoc/>
        public IReadOnlyList<string> GetRoots()
        {
            return _Roots;
        }

        /// <inheritdoc/>
        public IReadOnlyList<string> GetChildren(string path, bool includeFiles, bool includeHidden)
        {
            _Calls[path] = CallCountFor(path) + 1;

            if (!_Children.TryGetValue(path, out string[]? kids))
                return Array.Empty<string>();

            List<string> result = new List<string>();
            for (int i = 0; i < kids.Length; i++)
            {
                string child = kids[i];
                if (!includeFiles && !IsDirectory(child))
                    continue;
                if (!includeHidden && IsHidden(child))
                    continue;

                result.Add(child);
            }

            return result;
        }

        /// <inheritdoc/>
        public bool IsDirectory(string path)
        {
            return _Children.ContainsKey(path);
        }

        /// <inheritdoc/>
        public string DisplayName(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            int slash = path.LastIndexOf('/');
            return slash >= 0 && slash < path.Length - 1 ? path.Substring(slash + 1) : path;
        }

        /// <inheritdoc/>
        public string Normalize(string path)
        {
            return path ?? string.Empty;
        }

        private static bool IsHidden(string path)
        {
            int slash = path.LastIndexOf('/');
            string name = slash >= 0 && slash < path.Length - 1 ? path.Substring(slash + 1) : path;
            return name.Length > 0 && name[0] == '.';
        }
    }
}

namespace TUIKit.Modals
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Widgets;

    /// <summary>
    /// A hierarchical file/folder selector: a bordered dialog wrapping a <see cref="CheckTree{T}"/> over
    /// absolute paths, with a file-system provider and result mapping. Checking a folder includes its
    /// whole subtree; unchecking a descendant carves a hole; the result is the set of top-most included
    /// paths plus the excluded holes. Space toggles; Enter finishes; Escape cancels. The result is
    /// delivered through <see cref="Modal.Completion"/> / <c>ShowAsync&lt;FileSelection&gt;</c>: a
    /// <see cref="FileSelection"/> on finish, or <c>null</c> on cancel. Not thread-safe; drive it from
    /// the UI loop.
    /// </summary>
    public sealed class FileSelectModal : DialogModal
    {
        private readonly FileSelectOptions _Options;
        private readonly IFileSystemProvider _Provider;
        private readonly IEqualityComparer<string> _Comparer;
        private readonly StringComparison _Comparison;
        private readonly char _Separator = System.IO.Path.DirectorySeparatorChar;
        private readonly char _AltSeparator = System.IO.Path.AltDirectorySeparatorChar;
        private readonly CheckTree<string> _Tree;

        /// <summary>
        /// Initializes a new instance of the <see cref="FileSelectModal"/> class.
        /// </summary>
        /// <param name="options">Roots, pre-checks, and listing switches; defaults are applied when null.</param>
        /// <param name="provider">The file-system provider; defaults to <see cref="FileSystemProvider"/> (the real disk) when null.</param>
        public FileSelectModal(FileSelectOptions? options = null, IFileSystemProvider? provider = null)
        {
            _Options = options ?? new FileSelectOptions();
            _Provider = provider ?? new FileSystemProvider();
            _Comparer = _Provider.PathComparer;
            _Comparison = _Comparer.Equals("a", "A") ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

            IReadOnlyList<string> roots = _Options.Roots != null && _Options.Roots.Count > 0
                ? _Options.Roots
                : _Provider.GetRoots();

            _Tree = new CheckTree<string>(
                roots,
                path => _Provider.GetChildren(path, _Options.ShowFiles, _Options.ShowHidden),
                path => _Provider.DisplayName(path),
                path => _Provider.IsDirectory(path),
                _Comparer);

            Title = _Options.Title;
            FooterHint = "Space: toggle · Enter: done · Esc: cancel";
            MinContentWidth = 30;
            MinContentHeight = 6;

            PreSeed();
        }

        /// <inheritdoc/>
        public override bool HandleKey(KeyEvent key)
        {
            if (key.Code == KeyCode.Enter)
            {
                Finish();
                return true;
            }

            if (key.Code == KeyCode.Escape)
                return HandleDismiss(key, null);

            return _Tree.HandleKey(key);
        }

        /// <inheritdoc/>
        protected override int MeasureContentWidth(int availableWidth)
        {
            return Math.Min(availableWidth, 72);
        }

        /// <inheritdoc/>
        protected override int MeasureContentHeight(int contentWidth)
        {
            Size measured = _Tree.Measure(new Size(contentWidth, int.MaxValue));
            int rows = Math.Max(6, Math.Min(measured.Height, 20));
            return rows;
        }

        /// <inheritdoc/>
        protected override void RenderContent(ISurface content)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));

            _Tree.Render(content);
        }

        private void Finish()
        {
            List<string> includes = new List<string>();
            IReadOnlyList<string> includedRoots = _Tree.IncludedRoots();
            for (int i = 0; i < includedRoots.Count; i++)
                includes.Add(includedRoots[i]);

            List<FileExclusion> excludes = new List<FileExclusion>();
            IReadOnlyList<string> holes = _Tree.ExcludedHoles();
            for (int i = 0; i < holes.Count; i++)
                excludes.Add(new FileExclusion(holes[i], _Provider.IsDirectory(holes[i])));

            Close(new FileSelection(includes, excludes));
        }

        private void PreSeed()
        {
            string? firstInclude = null;

            if (_Options.PreCheckedIncludes != null)
            {
                foreach (string include in _Options.PreCheckedIncludes)
                {
                    string normalized = _Provider.Normalize(include);
                    if (RevealPath(normalized))
                    {
                        _Tree.SetExplicit(normalized, true);
                        if (firstInclude == null)
                            firstInclude = normalized;
                    }
                }
            }

            if (_Options.PreCheckedExcludes != null)
            {
                foreach (string exclude in _Options.PreCheckedExcludes)
                {
                    string normalized = _Provider.Normalize(exclude);
                    if (RevealPath(normalized))
                        _Tree.SetExplicit(normalized, false);
                }
            }

            if (firstInclude != null)
                _Tree.RevealTo(firstInclude);
        }

        private bool RevealPath(string path)
        {
            string? root = FindRoot(path);
            if (root == null)
            {
                // Non-path-structured nodes (for example an in-memory test tree): fall back to a search.
                _Tree.RevealTo(path);
                return true;
            }

            _Tree.Expand(root);
            if (_Comparer.Equals(root, path))
                return true;

            string current = root;
            while (true)
            {
                IReadOnlyList<string> children = _Provider.GetChildren(current, _Options.ShowFiles, _Options.ShowHidden);
                string? next = null;
                foreach (string child in children)
                {
                    string normalizedChild = _Provider.Normalize(child);
                    if (_Comparer.Equals(normalizedChild, path))
                        return true;

                    if (IsAncestor(normalizedChild, path))
                    {
                        next = child;
                        break;
                    }
                }

                if (next == null)
                    return false;

                _Tree.Expand(next);
                current = _Provider.Normalize(next);
            }
        }

        private string? FindRoot(string path)
        {
            IReadOnlyList<string> roots = _Options.Roots != null && _Options.Roots.Count > 0
                ? _Options.Roots
                : _Provider.GetRoots();

            for (int i = 0; i < roots.Count; i++)
            {
                string normalizedRoot = _Provider.Normalize(roots[i]);
                if (_Comparer.Equals(normalizedRoot, path) || IsAncestor(normalizedRoot, path))
                    return roots[i];
            }

            return null;
        }

        private bool IsAncestor(string ancestor, string descendant)
        {
            if (_Comparer.Equals(ancestor, descendant))
                return false;

            if (!descendant.StartsWith(ancestor, _Comparison))
                return false;

            if (EndsWithSeparator(ancestor))
                return true;

            if (descendant.Length <= ancestor.Length)
                return false;

            char boundary = descendant[ancestor.Length];
            return boundary == _Separator || boundary == _AltSeparator;
        }

        private bool EndsWithSeparator(string path)
        {
            if (path.Length == 0)
                return false;

            char last = path[path.Length - 1];
            return last == _Separator || last == _AltSeparator;
        }
    }
}

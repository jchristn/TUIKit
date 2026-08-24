namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for <see cref="CheckTree{T}"/>: cascading tri-state selection, hole reporting, result
    /// derivation, reveal-to, comparer-keyed state, the lazy-cache contract, and argument guards.
    /// </summary>
    public static class CheckTreeSuite
    {
        /// <summary>
        /// Builds the check-tree suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "CheckTree",
                displayName: "CheckTree (cascading tri-state selection)",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("CheckTree", "CheckFolderCascades", "Checking a folder makes its children EffectiveChecked",
                        _ =>
                        {
                            CheckTree<string> tree = Build();
                            tree.ToggleAt("src");
                            Check.True(tree.EffectiveChecked("src"), "src checked");
                            Check.True(tree.EffectiveChecked("a"), "child a inherits checked");
                            Check.True(tree.EffectiveChecked("b"), "child b inherits checked");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("CheckTree", "UncheckDescendantMakesHole", "Unchecking a child leaves the parent Partial and the sibling checked",
                        _ =>
                        {
                            CheckTree<string> tree = Build();
                            tree.ToggleAt("src");
                            tree.ToggleAt("a");
                            Check.False(tree.EffectiveChecked("a"), "a unchecked");
                            Check.True(tree.EffectiveChecked("b"), "b still checked");
                            Check.Equal(CheckState.Partial, tree.State("src"), "src partial");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("CheckTree", "IncludedRootsTopMost", "IncludedRoots returns the folder, not each child",
                        _ =>
                        {
                            CheckTree<string> tree = Build();
                            tree.Expand("root");
                            tree.ToggleAt("src");
                            IReadOnlyList<string> includes = tree.IncludedRoots();
                            Check.Equal(1, includes.Count, "one top-most include");
                            Check.Equal("src", includes[0], "the folder itself");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("CheckTree", "ExcludedHolesReported", "ExcludedHoles returns the hole under a checked ancestor",
                        _ =>
                        {
                            CheckTree<string> tree = Build();
                            tree.Expand("root");
                            tree.ToggleAt("src");
                            tree.ToggleAt("a");
                            IReadOnlyList<string> holes = tree.ExcludedHoles();
                            Check.Equal(1, holes.Count, "one hole");
                            Check.Equal("a", holes[0], "the unchecked child");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("CheckTree", "RevealToExpandsChain", "RevealTo loads and expands every ancestor",
                        _ =>
                        {
                            CheckTree<string> tree = Build();
                            tree.RevealTo("a");
                            Check.True(tree.IsExpanded("root"), "root expanded");
                            Check.True(tree.IsExpanded("src"), "src expanded");
                            Check.Equal("a", tree.SelectedNode, "cursor lands on a");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("CheckTree", "StateSurvivesRegeneratedNodes", "Regenerated equal nodes keep their checks via the comparer",
                        _ =>
                        {
                            CheckTree<string> tree = Build();
                            tree.Expand("root");
                            tree.ToggleAt(string.Concat("sr", "c"));
                            Check.True(tree.EffectiveChecked(string.Concat("s", "rc")), "a value-equal node reports the same check");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("CheckTree", "PartialOnlyFromLoaded", "An unopened branch never forces Partial",
                        _ =>
                        {
                            CheckTree<string> tree = Build();
                            tree.ToggleAt("root");
                            Check.Equal(CheckState.Checked, tree.State("root"), "root fully checked though src is unopened");
                            Check.Equal(CheckState.Checked, tree.State("src"), "unopened src inherits checked, not partial");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("CheckTree", "SpaceTogglesEnterIgnored", "Space toggles the selection and Enter is not consumed",
                        _ =>
                        {
                            CheckTree<string> tree = Build();
                            Check.True(tree.HandleKey(KeyEvent.Char(' ')), "space consumed");
                            Check.True(tree.EffectiveChecked("root"), "space toggled the selected root");
                            Check.False(tree.HandleKey(KeyEvent.Special(KeyCode.Enter)), "Enter is left for the host");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("CheckTree", "LazyChildrenCachedOncePerNode", "The child provider is invoked once per node across many renders",
                        _ =>
                        {
                            Dictionary<string, int> calls = new Dictionary<string, int>(StringComparer.Ordinal);
                            Dictionary<string, string[]> map = Map();
                            CheckTree<string> tree = new CheckTree<string>(
                                new[] { "root" },
                                node =>
                                {
                                    calls[node] = (calls.TryGetValue(node, out int c) ? c : 0) + 1;
                                    return map.TryGetValue(node, out string[]? kids) ? kids : Array.Empty<string>();
                                },
                                node => node,
                                node => map.ContainsKey(node));

                            tree.Expand("root");
                            tree.Expand("src");
                            for (int i = 0; i < 5; i++)
                                Render(tree, 20, 8);

                            Check.Equal(1, calls["root"], "root children resolved once");
                            Check.Equal(1, calls["src"], "src children resolved once");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("CheckTree", "EmptyRootsThrows", "Empty roots is rejected",
                        _ =>
                        {
                            Check.Throws<ArgumentException>(
                                () => new CheckTree<string>(Array.Empty<string>(), n => Array.Empty<string>(), n => n),
                                "empty roots");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("CheckTree", "NullArgsThrow", "Null roots, children, or label are rejected",
                        _ =>
                        {
                            Check.Throws<ArgumentNullException>(
                                () => new CheckTree<string>(null!, n => Array.Empty<string>(), n => n), "null roots");
                            Check.Throws<ArgumentNullException>(
                                () => new CheckTree<string>(new[] { "root" }, null!, n => n), "null children");
                            Check.Throws<ArgumentNullException>(
                                () => new CheckTree<string>(new[] { "root" }, n => Array.Empty<string>(), null!), "null label");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("CheckTree", "UnreadableChildrenTolerated", "A throwing child provider yields an empty child set",
                        _ =>
                        {
                            CheckTree<string> tree = new CheckTree<string>(
                                new[] { "root" },
                                node => throw new IOException("permission denied"),
                                node => node,
                                node => true);

                            tree.Expand("root");
                            Render(tree, 20, 4);
                            Check.Equal(CheckState.Unchecked, tree.State("root"), "no crash; root is a leaf-in-effect");
                            return Task.CompletedTask;
                        })
                });
        }

        private static Dictionary<string, string[]> Map()
        {
            return new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                { "root", new[] { "docs", "src", "bin" } },
                { "src", new[] { "a", "b" } }
            };
        }

        private static CheckTree<string> Build()
        {
            Dictionary<string, string[]> map = Map();
            return new CheckTree<string>(
                new[] { "root" },
                node => map.TryGetValue(node, out string[]? kids) ? kids : Array.Empty<string>(),
                node => node,
                node => map.ContainsKey(node));
        }

        private static void Render(IWidget widget, int width, int height)
        {
            CellBuffer buffer = new CellBuffer(width, height);
            widget.Render(new BufferSurface(buffer));
        }
    }
}

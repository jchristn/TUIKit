namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Modals;
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for <see cref="FileSelectModal"/> over a deterministic in-memory provider: pre-seeding
    /// and reveal, result mapping to <see cref="FileSelection"/>, cancel, the files/hidden switches, and
    /// default roots.
    /// </summary>
    public static class FileSelectModalSuite
    {
        /// <summary>
        /// Builds the file-select modal suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "FileSelectModal",
                displayName: "FileSelectModal (hierarchical file selection)",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("FileSelectModal", "PreSeedRevealsIncludesAndHoles", "Opens with the include checked, the hole cleared, and ancestors partial",
                        _ =>
                        {
                            FileSelectOptions options = new FileSelectOptions();
                            options.PreCheckedIncludes = new[] { "code" };
                            options.PreCheckedExcludes = new[] { "app" };
                            FileSelectModal modal = new FileSelectModal(options, Provider());

                            List<string> rows = RenderRows(modal);
                            Check.True(RowWith(rows, "code").Contains("[~]"), "code partial (has a hole)");
                            Check.True(RowWith(rows, "app").Contains("[ ]"), "app hole cleared");
                            Check.True(RowWith(rows, "lib").Contains("[x]"), "lib inherits checked");
                            Check.True(RowWith(rows, "root").Contains("[~]"), "root ancestor partial");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FileSelectModal", "ResultMapsToFileSelection", "Finish maps includes and excludes with the directory flag",
                        async _ =>
                        {
                            FileSelectOptions options = new FileSelectOptions();
                            options.PreCheckedIncludes = new[] { "code" };
                            options.PreCheckedExcludes = new[] { "app", "lib" };
                            FileSelectModal modal = new FileSelectModal(options, Provider());

                            modal.HandleKey(KeyEvent.Special(KeyCode.Enter));
                            FileSelection selection = (FileSelection)(await modal.Completion.ConfigureAwait(false))!;

                            Check.Equal(1, selection.Includes.Count, "one include");
                            Check.Equal("code", selection.Includes[0], "the included folder");
                            Check.Equal(2, selection.Excludes.Count, "two holes");
                            Check.True(FindExclusion(selection, "app").IsDirectory, "app is a directory");
                            Check.False(FindExclusion(selection, "lib").IsDirectory, "lib is a file");
                        }),

                    new TestCaseDescriptor("FileSelectModal", "CancelReturnsNull", "Escape closes with null",
                        async _ =>
                        {
                            FileSelectModal modal = new FileSelectModal(new FileSelectOptions(), Provider());
                            modal.HandleKey(KeyEvent.Special(KeyCode.Escape));
                            object? result = await modal.Completion.ConfigureAwait(false);
                            Check.True(result == null, "cancel yields null");
                        }),

                    new TestCaseDescriptor("FileSelectModal", "ShowFilesFalseHidesFiles", "Folders-only listing when ShowFiles is false",
                        _ =>
                        {
                            FileSelectOptions options = new FileSelectOptions();
                            options.ShowFiles = false;
                            FileSelectModal modal = new FileSelectModal(options, Provider());
                            modal.HandleKey(KeyEvent.Special(KeyCode.Right)); // expand root

                            List<string> rows = RenderRows(modal);
                            Check.True(AnyRow(rows, "code"), "directory listed");
                            Check.False(AnyRow(rows, "docs"), "file hidden when ShowFiles is false");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FileSelectModal", "RootsDefaultToDrives", "Default roots come from the provider",
                        _ =>
                        {
                            FileSelectModal modal = new FileSelectModal(new FileSelectOptions(), Provider());
                            List<string> rows = RenderRows(modal);
                            Check.True(AnyRow(rows, "root"), "provider root shown");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FileSelectModal", "PreSeedMissingPathIgnored", "A pre-checked path that does not exist is skipped without throwing",
                        async _ =>
                        {
                            FileSelectOptions options = new FileSelectOptions();
                            options.PreCheckedIncludes = new[] { "ghost" };
                            FileSelectModal modal = new FileSelectModal(options, Provider());
                            modal.HandleKey(KeyEvent.Special(KeyCode.Enter));
                            FileSelection selection = (FileSelection)(await modal.Completion.ConfigureAwait(false))!;
                            Check.Equal(0, selection.Includes.Count, "missing pre-seed ignored");
                        }),

                    new TestCaseDescriptor("FileSelectModal", "NoSelectionFinishEmpty", "Finishing with nothing checked yields empty includes",
                        async _ =>
                        {
                            FileSelectModal modal = new FileSelectModal(new FileSelectOptions(), Provider());
                            modal.HandleKey(KeyEvent.Special(KeyCode.Enter));
                            FileSelection selection = (FileSelection)(await modal.Completion.ConfigureAwait(false))!;
                            Check.Equal(0, selection.Includes.Count, "no includes");
                            Check.Equal(0, selection.Excludes.Count, "no excludes");
                        })
                });
        }

        private static InMemoryFileSystemProvider Provider()
        {
            Dictionary<string, string[]> map = new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                { "root", new[] { "docs", "code", "bin" } },
                { "code", new[] { "app", "lib" } },
                { "app", Array.Empty<string>() }
            };
            return new InMemoryFileSystemProvider(new[] { "root" }, map);
        }

        private static FileExclusion FindExclusion(FileSelection selection, string path)
        {
            for (int i = 0; i < selection.Excludes.Count; i++)
            {
                if (selection.Excludes[i].Path == path)
                    return selection.Excludes[i];
            }

            throw new InvalidOperationException("Exclusion not found: " + path);
        }

        private static List<string> RenderRows(FileSelectModal modal)
        {
            CellBuffer buffer = new CellBuffer(50, 20);
            modal.Render(new BufferSurface(buffer));

            List<string> rows = new List<string>();
            for (int y = 0; y < buffer.Height; y++)
            {
                StringBuilder row = new StringBuilder(buffer.Width);
                for (int x = 0; x < buffer.Width; x++)
                    row.Append(buffer.Get(x, y).Grapheme);

                rows.Add(row.ToString());
            }

            return rows;
        }

        private static bool AnyRow(List<string> rows, string needle)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Contains(needle))
                    return true;
            }

            return false;
        }

        private static string RowWith(List<string> rows, string needle)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Contains(needle))
                    return rows[i];
            }

            return string.Empty;
        }
    }
}

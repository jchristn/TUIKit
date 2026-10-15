namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Input;
    using TUIKit.Testing;
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for 1.10 item C5: <see cref="DiffView.FromUnifiedDiff"/>, the new <see cref="DiffLineKind"/> members,
    /// <see cref="DiffView.Files"/>, file navigation, line numbers, <see cref="DiffView.PlainText"/>, and the 1.7
    /// rendering of the two-text constructor.
    /// </summary>
    public static class DiffViewUnifiedSuite
    {
        private const string _Id = "DiffViewUnified";

        internal const string TwoFiles =
            "diff --git a/src/app.cs b/src/app.cs\n" +
            "index 1a2b3c4..5d6e7f8 100644\n" +
            "--- a/src/app.cs\n" +
            "+++ b/src/app.cs\n" +
            "@@ -1,3 +1,3 @@ class App\n" +
            " using System;\n" +
            "-int port = 80;\n" +
            "+int port = 8080;\n" +
            " return port;\n" +
            "diff --git a/README.md b/README.md\n" +
            "--- a/README.md\n" +
            "+++ b/README.md\n" +
            "@@ -10,2 +10,3 @@\n" +
            " # Title\n" +
            "+A new line\n" +
            " end\n";

        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: _Id,
                displayName: "DiffView Unified Input",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(_Id, "TwoFiles", "A two-file git diff: kinds per line, files, markers kept, and styles per kind",
                        _ =>
                        {
                            DiffView view = DiffView.FromUnifiedDiff(TwoFiles);
                            DiffLineKind[] kinds = view.LineKinds.ToArray();
                            Check.Equal(DiffLineKind.FileHeader, kinds[0], "diff --git");
                            Check.Equal(DiffLineKind.Meta, kinds[1], "index");
                            Check.Equal(DiffLineKind.FileHeader, kinds[2], "---");
                            Check.Equal(DiffLineKind.FileHeader, kinds[3], "+++");
                            Check.Equal(DiffLineKind.HunkHeader, kinds[4], "@@");
                            Check.Equal(DiffLineKind.Context, kinds[5], "context");
                            Check.Equal(DiffLineKind.Removed, kinds[6], "removed");
                            Check.Equal(DiffLineKind.Added, kinds[7], "added");
                            Check.Equal("src/app.cs,README.md", string.Join(",", view.Files), "files");
                            Check.Equal(2, view.AddedCount, "added count");
                            Check.Equal(1, view.RemovedCount, "removed count");
                            CellBuffer buffer = MarkdownThemeSuite.Draw(view, 40, 16);
                            string[] lines = Snapshot.ToText(buffer).Split('\n');
                            Check.Equal("-int port = 80;", lines[6], "removed marker kept");
                            Check.Equal("+int port = 8080;", lines[7], "added marker kept");
                            Check.True((buffer.Get(0, 0).Style.Attributes & CellAttributes.Bold) != 0, "file header bold");
                            Check.Equal(Color.FromPalette(6), buffer.Get(0, 4).Style.Foreground, "hunk header palette 6");
                            Check.True((buffer.Get(0, 1).Style.Attributes & CellAttributes.Dim) != 0, "meta dim");
                            Check.Equal(Color.FromPalette(2), buffer.Get(0, 7).Style.Foreground, "added green");
                            Check.Equal(TwoFiles, view.PlainText, "PlainText is the input");
                            Check.Throws<ArgumentNullException>(() => DiffView.FromUnifiedDiff(null!), "null diff");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "BodyLinesThatLookLikeHeaders", "A removed '--- x' and an added '+++ y' inside a hunk stay body lines; counts default to 1",
                        _ =>
                        {
                            string diff = "--- a/q.sql\n+++ b/q.sql\n@@ -1,2 +1,2 @@\n--- old comment\n+++ new comment\n same\n@@ -9 +9 @@\n-x\n+y\n";
                            DiffView view = DiffView.FromUnifiedDiff(diff);
                            DiffLineKind[] kinds = view.LineKinds.ToArray();
                            Check.Equal(DiffLineKind.Removed, kinds[3], "'--- old comment' is removed");
                            Check.Equal(DiffLineKind.Added, kinds[4], "'+++ new comment' is added");
                            Check.Equal(DiffLineKind.Context, kinds[5], "context");
                            Check.Equal(DiffLineKind.HunkHeader, kinds[6], "a hunk without counts");
                            Check.Equal(DiffLineKind.Removed, kinds[7], "count 1 old");
                            Check.Equal(DiffLineKind.Added, kinds[8], "count 1 new");
                            Check.Equal(1, view.Files.Count, "one file");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Metadata", "Rename, new, deleted, binary, and no-newline lines are metadata; /dev/null gives the right paths; CRLF works",
                        _ =>
                        {
                            string diff =
                                "diff --git a/old.txt b/new.txt\r\nsimilarity index 90%\r\nrename from old.txt\r\nrename to new.txt\r\n" +
                                "diff --git a/added.txt b/added.txt\r\nnew file mode 100644\r\n--- /dev/null\r\n+++ b/added.txt\r\n@@ -0,0 +1 @@\r\n+hello\r\n\\ No newline at end of file\r\n" +
                                "diff --git a/gone.txt b/gone.txt\r\ndeleted file mode 100644\r\n--- a/gone.txt\r\n+++ /dev/null\r\n@@ -1 +0,0 @@\r\n-bye\r\n" +
                                "diff --git a/logo.png b/logo.png\r\nBinary files a/logo.png and b/logo.png differ\r\n";
                            DiffView view = DiffView.FromUnifiedDiff(diff);
                            Check.Equal("new.txt,added.txt,gone.txt,logo.png", string.Join(",", view.Files), "paths, including a deleted file's old path");
                            DiffLineKind[] kinds = view.LineKinds.ToArray();
                            Check.Equal(DiffLineKind.Meta, kinds[1], "similarity");
                            Check.Equal(DiffLineKind.Meta, kinds[2], "rename from");
                            Check.Equal(DiffLineKind.Meta, kinds[5], "new file mode");
                            Check.Equal(DiffLineKind.Added, kinds[9], "added line");
                            Check.Equal(DiffLineKind.Meta, kinds[10], "no newline inside a hunk");
                            Check.Equal(DiffLineKind.Meta, kinds[kinds.Length - 1], "binary");
                            Check.False(Snapshot.RenderWidget(view, 60, 30).Contains("\r"), "CR stripped");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "QuotedAndCombined", "Quoted paths unquote, octal escapes decode, and a combined diff renders literally",
                        _ =>
                        {
                            string quoted = "diff --git \"a/my file.txt\" \"b/my file.txt\"\n--- \"a/my file.txt\"\n+++ \"b/my \\303\\251t\\303\\251.txt\"\n@@ -1 +1 @@\n-a\n+b\n";
                            Check.Equal("my été.txt", DiffView.FromUnifiedDiff(quoted).Files[0], "quoted with octal UTF-8");
                            DiffView git = DiffView.FromUnifiedDiff("diff --git \"a/x y\" \"b/x y\"\nBinary files differ\n");
                            Check.Equal("x y", git.Files[0], "quoted diff --git path");
                            DiffView tabbed = DiffView.FromUnifiedDiff("--- old.c\t2026-01-01 10:00\n+++ new.c\t2026-01-02 10:00\n@@ -1 +1 @@\n-a\n+b\n");
                            Check.Equal("new.c", tabbed.Files[0], "diff -u timestamp stripped");
                            DiffView combined = DiffView.FromUnifiedDiff("diff --cc file.c\n@@@ -1,1 -1,1 +1,1 @@@\n- a\n -b\n++c\n");
                            Check.True(combined.LineKinds.All(k => k == DiffLineKind.Context || k == DiffLineKind.Meta), "combined diff literal");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Literal", "Plain text and a truncated hunk render literally without throwing",
                        _ =>
                        {
                            DiffView plain = DiffView.FromUnifiedDiff("just some text\nwith lines\n");
                            Check.True(plain.LineKinds.All(k => k == DiffLineKind.Context), "plain text is context");
                            Check.Equal(0, plain.Files.Count, "no files");
                            Check.Equal(-1, plain.CurrentFileIndex, "no current file");
                            DiffView truncated = DiffView.FromUnifiedDiff("--- a/x\n+++ b/x\n@@ -1,5 +1,5 @@\n context\n-gone\nnot a diff line\n");
                            Check.Equal(DiffLineKind.Context, truncated.LineKinds.Last(), "the stray line is context");
                            Snapshot.RenderWidget(truncated, 30, 10);
                            DiffView empty = DiffView.FromUnifiedDiff(string.Empty);
                            empty.ShowLineNumbers = true;
                            Snapshot.RenderWidget(empty, 30, 3);
                            Check.Equal(0, empty.LineCount, "empty");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Files", "NextFile, PreviousFile, GoToFile, and CurrentFileIndex; ] and [ only with UseFileKeys",
                        _ =>
                        {
                            DiffView view = DiffView.FromUnifiedDiff(TwoFiles);
                            Check.Equal(0, view.CurrentFileIndex, "first file");
                            Check.False(view.HandleKey(KeyEvent.Char(']')), "] not consumed by default");
                            Check.True(view.NextFile(), "next");
                            Check.Equal(9, view.ScrollOffset, "header at the top");
                            Check.Equal(1, view.CurrentFileIndex, "second file");
                            Check.False(view.NextFile(), "no later file");
                            Check.True(view.PreviousFile(), "previous");
                            Check.False(view.PreviousFile(), "no earlier file");
                            Check.True(view.GoToFile(1), "GoToFile");
                            Check.False(view.GoToFile(1), "already there");
                            Check.Throws<ArgumentOutOfRangeException>(() => view.GoToFile(2), "out of range");
                            view.UseFileKeys = true;
                            Check.True(view.HandleKey(KeyEvent.Char('[')), "[");
                            Check.Equal(0, view.CurrentFileIndex, "back to the first file");
                            Check.True(view.HandleKey(KeyEvent.Char(']')), "]");
                            Check.Equal(1, view.CurrentFileIndex, "second again");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "LineNumbers", "Line numbers come from hunk headers and from the computed constructor; headers are blank",
                        _ =>
                        {
                            DiffView view = DiffView.FromUnifiedDiff(TwoFiles);
                            view.ShowLineNumbers = true;
                            string[] lines = Snapshot.RenderWidget(view, 50, 16).Split('\n');
                            Check.Equal("      @@ -1,3 +1,3 @@ class App", lines[4], "header blank in the gutter");
                            Check.Equal(" 1  1  using System;", lines[5], "context numbers");
                            Check.Equal(" 2    -int port = 80;", lines[6], "removed: old only");
                            Check.Equal("    2 +int port = 8080;", lines[7], "added: new only");
                            Check.Equal("10 10  # Title", lines[13], "second hunk starts at 10");
                            DiffView computed = new DiffView("a\nb\n", "a\nc\n") { ShowLineNumbers = true };
                            string[] rows = Snapshot.RenderWidget(computed, 20, 5).Split('\n');
                            Check.Equal("1 1  a", rows[0], "computed context");
                            Check.Equal("2   -b", rows[1], "computed removed");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "ComputedUnchanged", "The two-text constructor renders exactly as in 1.7 and reports no files",
                        _ =>
                        {
                            DiffView view = new DiffView("one\ntwo\nthree", "one\n2\nthree");
                            CellBuffer buffer = MarkdownThemeSuite.Draw(view, 20, 5);
                            Check.Equal(" one\n-two\n+2\n three", Snapshot.ToText(buffer).TrimEnd(), "text");
                            Check.Equal(Color.FromPalette(1), buffer.Get(0, 1).Style.Foreground, "removed marker red");
                            Check.Equal(Color.FromPalette(2), buffer.Get(1, 2).Style.Foreground, "added text green");
                            Check.Equal(CellStyle.Default, buffer.Get(1, 0).Style, "context default");
                            Check.Equal(0, view.Files.Count, "no files");
                            Check.Equal(" one\n-two\n+2\n three", view.PlainText, "plain text with markers");
                            Check.False(view.LineKinds.Any(k => k >= DiffLineKind.FileHeader), "no new kinds");
                            Check.Equal(3, (int)DiffLineKind.FileHeader, "enum values appended");
                            return Task.CompletedTask;
                        }),
                });
        }
    }
}

namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Testing;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for 1.10 item C5: <see cref="DiffView"/> search, F3 keys, viewport members, paging, theme, and hints.
    /// </summary>
    public static class DiffViewSearchSuite
    {
        private const string _Id = "DiffViewSearch";

        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: _Id,
                displayName: "DiffView Search and Viewport",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(_Id, "Search", "Search is case-insensitive over the drawn text, markers included, and wraps both ways",
                        _ =>
                        {
                            DiffView view = Long();
                            Snapshot.RenderWidget(view, 30, 5);
                            view.SetSearch("+NEEDLE");
                            Check.Equal("+NEEDLE", view.SearchText, "SearchText");
                            Check.True(view.FindNext(), "found");
                            int first = view.ScrollOffset;
                            Check.True(Snapshot.RenderWidget(view, 30, 5).Contains("+needle 10"), "the match is visible");
                            Check.True(view.FindNext(), "second");
                            Check.True(view.ScrollOffset > first, "moved down");
                            Check.True(view.FindNext(), "wraps");
                            Check.Equal(first, view.ScrollOffset, "back to the first");
                            Check.True(view.FindPrevious(), "previous wraps");
                            Check.True(view.ScrollOffset > first, "to the last match");
                            CellBuffer buffer = MarkdownThemeSuite.Draw(view, 30, 5);
                            Check.True(Enumerable.Range(0, 5).Any(y => buffer.Get(0, y).Style.Background == Color.FromPalette(3)), "highlighted with the Pane default");
                            view.SetSearch(null);
                            Check.False(view.FindNext(), "cleared");
                            view.SetSearch("absent");
                            Check.False(view.FindNext(), "no match");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "F3", "F3 and Shift+F3 move between matches only while a search is set",
                        _ =>
                        {
                            DiffView view = Long();
                            Snapshot.RenderWidget(view, 30, 5);
                            Check.False(view.HandleKey(KeyEvent.Special(KeyCode.F3)), "not consumed without a search");
                            view.SetSearch("needle");
                            Check.True(view.HandleKey(KeyEvent.Special(KeyCode.F3)), "F3");
                            int first = view.ScrollOffset;
                            view.HandleKey(KeyEvent.Special(KeyCode.F3));
                            view.HandleKey(KeyEvent.Special(KeyCode.F3, KeyModifiers.Shift));
                            Check.Equal(first, view.ScrollOffset, "Shift+F3 back");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Paging", "PageByViewport false keeps 10-row paging; true pages by the viewport minus one",
                        _ =>
                        {
                            DiffView view = Long();
                            Snapshot.RenderWidget(view, 30, 5);
                            view.HandleKey(KeyEvent.Special(KeyCode.PageDown));
                            Check.Equal(10, view.ScrollOffset, "ten rows (1.7)");
                            view.PageByViewport = true;
                            view.HandleKey(KeyEvent.Special(KeyCode.PageDown));
                            Check.Equal(14, view.ScrollOffset, "viewport 5 minus one");
                            Check.Equal(5, view.ViewportHeight, "ViewportHeight");
                            view.ScrollBy(-100);
                            Check.Equal(0, view.ScrollOffset, "clamped at the top");
                            view.ScrollToEnd();
                            Check.Equal(view.LineCount - 5, view.ScrollOffset, "end");
                            view.ScrollToTop();
                            Check.Equal(0, view.ScrollOffset, "top");
                            view.ScrollOffset = 9999;
                            Check.Equal(view.LineCount - 1, view.ScrollOffset, "setter clamps like Down");
                            Check.Equal(view.LineCount, view.ContentHeight, "ContentHeight");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Theme", "UseThemeStyles false ignores the theme's colors; true follows it; the search role applies once a theme is given",
                        _ =>
                        {
                            DiffView view = DiffView.FromUnifiedDiff(DiffViewUnifiedSuite.TwoFiles);
                            CellBuffer before = MarkdownThemeSuite.Draw(view, 40, 10);
                            Theme theme = Theme.Light;
                            view.ApplyTheme(theme);
                            Check.True(MarkdownThemeSuite.SameCells(before, MarkdownThemeSuite.Draw(view, 40, 10)), "unchanged while off");
                            view.UseThemeStyles = true;
                            CellBuffer themed = MarkdownThemeSuite.Draw(view, 40, 10);
                            Check.Equal(theme.Success, themed.Get(1, 7).Style, "added from Success");
                            Check.Equal(theme.Error, themed.Get(1, 6).Style, "removed from Error");
                            Check.Equal(theme.Info, themed.Get(0, 4).Style, "hunk header from Info");
                            view.SetSearch("port");
                            view.FindNext();
                            CellBuffer searched = MarkdownThemeSuite.Draw(view, 40, 10);
                            CellStyle role = theme.Resolve(Theme.SearchMatchRole, CellStyle.Default);
                            Check.True(Enumerable.Range(0, 40).Any(x => searched.Get(x, 6).Style == role), "search role used");
                            Check.Throws<ArgumentNullException>(() => view.ApplyTheme(null!), "null theme");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Hints", "Hints appear only while focused and scrollable; file and search hints when they apply; ShowKeyHints false hides them",
                        _ =>
                        {
                            DiffView view = Long();
                            Snapshot.RenderWidget(view, 30, 5);
                            Check.True(view.GetKeyHints() == null, "not focused");
                            view.OnFocusChanged(true);
                            Check.True(view.IsFocused, "focused");
                            Check.Equal("Scroll", string.Join(",", view.GetKeyHints()!.Select(h => h.Description)), "scroll");
                            view.SetSearch("needle");
                            Check.True(view.GetKeyHints()!.Any(h => h.Description == "Next match"), "F3 hint");
                            view.ShowKeyHints = false;
                            Check.True(view.GetKeyHints() == null, "hidden");
                            DiffView files = DiffView.FromUnifiedDiff(DiffViewUnifiedSuite.TwoFiles);
                            files.UseFileKeys = true;
                            files.OnFocusChanged(true);
                            Snapshot.RenderWidget(files, 40, 30);
                            Check.Equal("File", string.Join(",", files.GetKeyHints()!.Select(h => h.Description)), "a short two-file diff shows only the file hint");
                            files.SetSearch("port");
                            files.FindNext();
                            Check.True(files.TryGetFocusRect(out Rect rect) && rect.Y == 6, "focus rect on the match");
                            return Task.CompletedTask;
                        }),
                });
        }

        private static DiffView Long()
        {
            StringBuilder diff = new StringBuilder("--- a/x\n+++ b/x\n@@ -1,40 +1,40 @@\n");
            for (int i = 1; i <= 40; i++)
                diff.Append(i == 10 || i == 30 ? "+needle " + i + "\n" : " line " + i + "\n");
            return DiffView.FromUnifiedDiff(diff.ToString());
        }
    }
}

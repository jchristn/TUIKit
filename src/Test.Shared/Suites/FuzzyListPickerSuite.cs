namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Testing;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for 1.8 item C4: <see cref="FuzzyList{T}"/> styles and theme, grapheme Backspace, paste, line-editing
    /// keys, detail text, disabled items, the provider, activation, and key hints.
    /// </summary>
    public static class FuzzyListPickerSuite
    {
        private const string _Id = "FuzzyListPicker";

        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: _Id,
                displayName: "FuzzyList Pickers",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(_Id, "DefaultRendering", "With defaults the list draws the 1.7 prompt, colors, and rows",
                        _ =>
                        {
                            FuzzyList<string> list = new FuzzyList<string>(new[] { "alpha", "beta", "gamma" });
                            list.Query = "a";
                            CellBuffer buffer = MarkdownThemeSuite.Draw(list, 12, 4);
                            string[] lines = Snapshot.ToText(buffer).Split('\n').Select(l => l.TrimEnd()).ToArray();
                            Check.Equal("> a|alpha|gamma|beta", string.Join("|", lines.Take(4)), "text");
                            Check.Equal(CellStyle.Default.WithForeground(Color.FromPalette(6)), buffer.Get(0, 0).Style, "prompt in palette 6");
                            Check.Equal(CellStyle.Default.WithForeground(Color.FromRgb(0, 0, 0)).WithBackground(Color.FromPalette(6)), buffer.Get(1, 1).Style, "selected row highlight");
                            Check.Equal(CellStyle.Default, buffer.Get(0, 2).Style, "unmatched cell in default style");
                            Check.Equal(CellStyle.Default.WithForeground(Color.FromPalette(3)).WithAttribute(CellAttributes.Bold, true), buffer.Get(1, 2).Style, "matched cell in match style");
                            Check.True(list.GetKeyHints() != null, "hints on by default");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Backspace", "Backspace after an emoji leaves no lone surrogate",
                        _ =>
                        {
                            FuzzyList<string> list = new FuzzyList<string>(new[] { "x" });
                            list.Query = "a\U0001F600";
                            list.HandleKey(KeyEvent.Special(KeyCode.Backspace));
                            Check.Equal("a", list.Query, "whole emoji removed");
                            list.HandleKey(KeyEvent.Special(KeyCode.Backspace));
                            list.HandleKey(KeyEvent.Special(KeyCode.Backspace));
                            Check.Equal(string.Empty, list.Query, "empty stays empty");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Insert", "Insert appends, flattens newlines, and ignores null; HandlePaste rejects null",
                        _ =>
                        {
                            FuzzyList<string> list = new FuzzyList<string>(new[] { "x" });
                            list.Insert("ab\ncd");
                            Check.Equal("ab cd", list.Query, "newline became a space");
                            list.Insert(null);
                            list.Insert(string.Empty);
                            Check.Equal("ab cd", list.Query, "null and empty ignored");
                            Check.True(list.HandlePaste("!"), "paste consumed");
                            Check.Equal("ab cd!", list.Query, "pasted");
                            Check.Throws<ArgumentNullException>(() => list.HandlePaste(null!), "null rejected");
                            Check.True(list.AcceptsText, "accepts text");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "LineEditingKeys", "Ctrl+U and Ctrl+W are left alone by default and edit the query when on",
                        _ =>
                        {
                            FuzzyList<string> list = new FuzzyList<string>(new[] { "x" });
                            list.Query = "foo bar";
                            Check.False(list.HandleKey(KeyEvent.Char('u', KeyModifiers.Ctrl)), "Ctrl+U not consumed");
                            Check.False(list.ConsumesChord(KeyChord.Parse("ctrl+u")), "not reported");
                            Check.Equal("foo bar", list.Query, "unchanged");
                            list.UseLineEditingKeys = true;
                            Check.True(list.ConsumesChord(KeyChord.Parse("ctrl+w")), "reported");
                            Check.True(list.HandleKey(KeyEvent.Char('w', KeyModifiers.Ctrl)), "Ctrl+W");
                            Check.Equal("foo ", list.Query, "last word deleted");
                            Check.True(list.HandleKey(KeyEvent.Char('u', KeyModifiers.Ctrl)), "Ctrl+U");
                            Check.Equal(string.Empty, list.Query, "cleared");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Detail", "Detail is drawn after the label, truncated first, and searched only with SearchDetail",
                        _ =>
                        {
                            FuzzyList<string> list = new FuzzyList<string>(new[] { "main", "dev" });
                            list.Detail = s => s == "main" ? "origin/main" : "zz-local";
                            string wide = Snapshot.RenderWidget(list, 30, 3);
                            Check.True(wide.Contains("main  origin/main"), "detail after two spaces: " + wide);
                            Check.Equal(Color.FromPalette(8), MarkdownThemeSuite.Draw(list, 30, 3).Get(8, 2).Style.Foreground, "detail style on an unselected row");
                            string narrow = Snapshot.RenderWidget(list, 10, 3);
                            Check.True(narrow.Contains("main  orig"), "detail truncated: " + narrow);
                            string tiny = Snapshot.RenderWidget(list, 5, 3);
                            Check.True(tiny.Contains("main"), "label kept when there is no room for detail");
                            list.Query = "zz";
                            Check.Equal(0, list.MatchCount, "detail not searched by default");
                            list.SearchDetail = true;
                            Check.Equal(1, list.MatchCount, "detail searched");
                            Check.Equal("dev", list.SelectedItem, "matched by detail");
                            FuzzyList<string> rank = new FuzzyList<string>(new[] { "xa", "ab" }) { Detail = s => s == "xa" ? "a" : "-" };
                            rank.SearchDetail = true;
                            rank.Query = "a";
                            Check.Equal("ab", rank.SelectedItem, "label match ranks above a detail-only match");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Disabled", "Navigation, clicks, and Enter skip or refuse disabled items; SelectedItem is never disabled",
                        _ =>
                        {
                            FuzzyList<string> list = new FuzzyList<string>(new[] { "a1", "a2", "a3", "a4" });
                            list.IsEnabled = s => s != "a1" && s != "a3";
                            Check.Equal("a2", list.SelectedItem, "first enabled selected");
                            list.HandleKey(KeyEvent.Special(KeyCode.Down));
                            Check.Equal("a4", list.SelectedItem, "Down skips a3");
                            list.HandleKey(KeyEvent.Special(KeyCode.Down));
                            Check.Equal("a4", list.SelectedItem, "Down at the end stays");
                            list.HandleKey(KeyEvent.Special(KeyCode.Up));
                            Check.Equal("a2", list.SelectedItem, "Up skips a3");
                            list.HandleKey(KeyEvent.Special(KeyCode.Up));
                            Check.Equal("a2", list.SelectedItem, "Up does not land on a1");
                            list.HandleKey(KeyEvent.Special(KeyCode.Home));
                            Check.Equal("a2", list.SelectedItem, "Home lands on the first enabled");
                            list.HandleKey(KeyEvent.Special(KeyCode.End));
                            Check.Equal("a4", list.SelectedItem, "End on the last enabled");
                            Snapshot.RenderWidget(list, 10, 5);
                            Check.False(list.HandleMouse(new MouseEvent(MouseEventKind.Press, MouseButton.Left, 0, 3, KeyModifiers.None, 1)), "click on a3 refused");
                            Check.Equal("a4", list.SelectedItem, "selection unchanged");
                            Check.True((MarkdownThemeSuite.Draw(list, 10, 5).Get(0, 1).Style.Attributes & CellAttributes.Dim) != 0, "disabled drawn dim");
                            list.IsEnabled = s => false;
                            Check.True(list.SelectedItem == null, "all disabled: default");
                            int activated = 0;
                            list.ItemActivated += (s, e) => activated++;
                            Check.False(list.HandleKey(KeyEvent.Special(KeyCode.Enter)), "Enter refused on no enabled item");
                            Check.Equal(0, activated, "not raised");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Provider", "ItemsProvider replaces items per query; a throwing provider keeps the old items and raises ProviderFailed",
                        _ =>
                        {
                            FuzzyList<string> list = new FuzzyList<string>(new[] { "fixed" });
                            bool fail = false;
                            list.ItemsProvider = q =>
                            {
                                if (fail)
                                    throw new InvalidOperationException("server down");
                                return new[] { "Create " + q, "existing" };
                            };
                            Check.Equal(2, list.MatchCount, "provider ran on assignment");
                            list.Query = "ex";
                            Check.Equal("existing", list.SelectedItem, "scored as usual");
                            list.Query = "new";
                            Check.Equal("Create new", list.SelectedItem, "computed entry");
                            Exception? seen = null;
                            list.ProviderFailed += (s, e) => seen = e;
                            fail = true;
                            list.Query = "n";
                            Check.True(seen is InvalidOperationException, "failure surfaced");
                            Check.Equal(2, list.MatchCount, "previous items kept");
                            Check.True(Snapshot.RenderWidget(list, 20, 3).Contains("Create new"), "the computed entry is still listed");
                            list.ItemsProvider = null;
                            Check.Equal(2, list.MatchCount, "items stay after removing the provider");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Activation", "Enter is consumed only with an ItemActivated handler; a double click activates",
                        _ =>
                        {
                            FuzzyList<string> list = new FuzzyList<string>(new[] { "one", "two" });
                            Check.False(list.HandleKey(KeyEvent.Special(KeyCode.Enter)), "no handler leaves Enter");
                            Check.False(list.ConsumesChord(KeyChord.Parse("enter")), "not reported");
                            List<string> activated = new List<string>();
                            list.ItemActivated += (s, item) => activated.Add(item);
                            Check.True(list.HandleKey(KeyEvent.Special(KeyCode.Enter)), "consumed");
                            Check.False(list.HandleKey(KeyEvent.Special(KeyCode.Enter, KeyModifiers.Shift)), "modified Enter not consumed");
                            Snapshot.RenderWidget(list, 10, 3);
                            list.HandleMouse(new MouseEvent(MouseEventKind.Press, MouseButton.Left, 1, 2, KeyModifiers.None, 2));
                            Check.Equal("one,two", string.Join(",", activated), "Enter and double click");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Theme", "UseThemeStyles false ignores the theme; true follows it and a later theme, and off restores",
                        _ =>
                        {
                            FuzzyList<string> list = new FuzzyList<string>(new[] { "a", "b" });
                            CellBuffer before = MarkdownThemeSuite.Draw(list, 8, 3);
                            Theme theme = Theme.Light;
                            list.ApplyTheme(theme);
                            Check.True(MarkdownThemeSuite.SameCells(before, MarkdownThemeSuite.Draw(list, 8, 3)), "unchanged while off");
                            list.UseThemeStyles = true;
                            CellBuffer themed = MarkdownThemeSuite.Draw(list, 8, 3);
                            Check.Equal(theme.Accent, themed.Get(0, 0).Style, "query from Accent");
                            Check.Equal(theme.Selection, themed.Get(0, 1).Style, "selection from Selection");
                            Check.Equal(theme.Text, themed.Get(0, 2).Style, "rows from Text");
                            Theme dark = Theme.Dark;
                            list.ApplyTheme(dark);
                            Check.Equal(dark.Accent, MarkdownThemeSuite.Draw(list, 8, 3).Get(0, 0).Style, "a later theme");
                            list.UseThemeStyles = false;
                            Check.True(MarkdownThemeSuite.SameCells(before, MarkdownThemeSuite.Draw(list, 8, 3)), "restored");
                            Check.Throws<ArgumentNullException>(() => list.ApplyTheme(null!), "null theme");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Hints", "Hints list Move, Choose with a handler, and the check keys with MultiSelect; ShowKeyHints false hides them",
                        _ =>
                        {
                            FuzzyList<string> list = new FuzzyList<string>(new[] { "a" });
                            Check.Equal("Move", string.Join(",", list.GetKeyHints()!.Select(h => h.Description)), "Move only");
                            list.ItemActivated += (s, e) => { };
                            list.MultiSelect = true;
                            Check.Equal("Choose,Move,Check,Check all", string.Join(",", list.GetKeyHints()!.Select(h => h.Description)), "all hints");
                            list.ShowKeyHints = false;
                            Check.True(list.GetKeyHints() == null, "hidden");
                            Check.Throws<ArgumentNullException>(() => list.QueryPrompt = null!, "null prompt rejected");
                            list.QueryPrompt = "find: ";
                            Check.True(Snapshot.RenderWidget(list, 12, 2).StartsWith("find:", StringComparison.Ordinal), "custom prompt");
                            return Task.CompletedTask;
                        }),
                });
        }
    }
}

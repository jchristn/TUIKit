namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Hosting;
    using TUIKit.Input;
    using TUIKit.Layout;
    using TUIKit.Terminal;
    using TUIKit.Testing;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for the <see cref="TabView"/> focused-tab treatment: <see cref="TabView.IsStripFocused"/>,
    /// <see cref="TabView.StripFocusStop"/>, <see cref="TabView.FocusedTabStyle"/>, and
    /// <see cref="TabView.TabFocusMarker"/>, by key, by mouse, and inside a host.
    /// </summary>
    public static class TabFocusSuite
    {
        /// <summary>
        /// Builds the tab focus suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "TabFocus",
                displayName: "TabView Focused Tab",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("TabFocus", "StripFocusedShowsMarker", "Without forwarding the strip holds focus and the selected tab shows the marker and cue",
                        _ =>
                        {
                            TabView tabs = Tabs(false, false);
                            tabs.OnFocusChanged(true);
                            WidgetTester tester = WidgetTester.For(tabs, 30, 4).Render();
                            Check.True(tabs.IsStripFocused, "strip focused");
                            Check.Equal(">", tester.CellAt(0, 0).Grapheme, "marker replaces the left padding");
                            CellAttributes attributes = tester.CellAt(1, 0).Style.Attributes;
                            Check.True((attributes & CellAttributes.Underline) != 0 && (attributes & CellAttributes.Bold) != 0, "bold underline cue");

                            tabs.OnFocusChanged(false);
                            tester.Render();
                            Check.False(tabs.IsStripFocused, "not focused");
                            Check.Equal(" ", tester.CellAt(0, 0).Grapheme, "no marker without focus");
                            Check.Equal(tabs.ActiveStyle, tester.CellAt(1, 0).Style, "plain active style without focus");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("TabFocus", "StripStopKeys", "With a strip stop, arrows switch tabs on the strip and Tab and Shift+Tab move between strip and content",
                        _ =>
                        {
                            TabView tabs = Tabs(true, true);
                            ListView<string> second = (ListView<string>)ContentAt(tabs, 1);
                            tabs.OnFocusChanged(true);
                            tabs.FocusEdge(true);
                            Check.True(tabs.IsStripFocused, "focus enters on the strip");
                            Check.True(tabs.FocusedChild == null, "no focused child while on the strip");
                            Check.True(ReferenceEquals(tabs, tabs.FocusedLeaf), "the tab view is the leaf");

                            Check.True(tabs.HandleKey(KeyEvent.Special(KeyCode.Right)), "Right consumed");
                            Check.Equal(1, tabs.ActiveIndex, "Right switched to the second tab");
                            Check.False(second.IsFocused, "content not focused while the strip is");

                            Check.True(tabs.HandleKey(KeyEvent.Special(KeyCode.Tab)), "Tab consumed");
                            Check.False(tabs.IsStripFocused, "Tab entered the content");
                            Check.True(ReferenceEquals(second, tabs.FocusedChild), "content is the focused child");
                            Check.True(second.IsFocused, "content told it has focus");

                            WidgetTester tester = WidgetTester.For(tabs, 30, 4).Render();
                            Check.False(tester.Row(0).Contains(">"), "no marker while content holds focus");

                            Check.True(tabs.HandleKey(KeyEvent.Special(KeyCode.Down)), "Down goes to the content list");
                            Check.True(tabs.HandleKey(KeyEvent.Special(KeyCode.Tab, KeyModifiers.Shift)), "Shift+Tab consumed");
                            Check.True(tabs.IsStripFocused, "Shift+Tab returned to the strip");
                            Check.False(second.IsFocused, "content told it lost focus");
                            Check.False(tabs.HandleKey(KeyEvent.Special(KeyCode.Tab, KeyModifiers.Shift)), "Shift+Tab on the strip bubbles out");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("TabFocus", "MouseFocus", "Clicking a tab focuses the strip; clicking the content focuses it",
                        _ =>
                        {
                            TabView tabs = Tabs(true, true);
                            tabs.OnFocusChanged(true);
                            tabs.FocusEdge(true);
                            WidgetTester tester = WidgetTester.For(tabs, 30, 4).Render();

                            tester.Click(1, 2);
                            Check.False(tabs.IsStripFocused, "content click focused the content");

                            tester.Click(7, 0);
                            Check.True(tabs.IsStripFocused, "tab click focused the strip");
                            Check.Equal(1, tabs.ActiveIndex, "tab click selected the second tab");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("TabFocus", "HostTraversal", "In a host, Tab goes strip, content, next region, and the path follows",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 8);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                TabView tabs = Tabs(true, true);
                                app.Layout = Layout.Create()
                                    .Add("tabs", r => r.ProportionalWidth(0, 0.5).FillHeight().WithPadding(0))
                                    .Add("other", r => r.ProportionalWidth(0.5, 0.5).FillHeight().WithPadding(0))
                                    .Build();
                                app.Bind("tabs", tabs);
                                app.Bind("other", new ListView<string>());
                                app.Start();
                                tabs.FocusEdge(true);
                                app.RenderOnce();
                                Check.True(tabs.IsStripFocused, "starts on the strip");
                                Check.Equal(1, app.CurrentFocusPath.Depth, "path ends at the tab view");

                                backend.FeedKey("tab");
                                app.PumpInputOnce();
                                Check.False(tabs.IsStripFocused, "Tab entered the content");
                                Check.Equal(2, app.CurrentFocusPath.Depth, "path reaches the content");

                                backend.FeedKey("tab");
                                app.PumpInputOnce();
                                Check.Equal("other", app.FocusedRegion, "Tab from the content left the region");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("TabFocus", "LegacyForwarding", "ForwardKeys without a strip stop keeps keys going to the content",
                        _ =>
                        {
                            TabView tabs = Tabs(true, false);
                            tabs.OnFocusChanged(true);
                            Check.False(tabs.IsStripFocused, "strip not focused under plain forwarding");
                            Check.True(tabs.FocusedChild != null, "content is the focused child");
                            Check.True(tabs.HandleKey(KeyEvent.Special(KeyCode.Down)), "Down reached the content");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("TabFocus", "ThemeAndOverrides", "ApplyTheme sets the focused-tab role style; an explicit style and an empty marker work",
                        _ =>
                        {
                            TabView tabs = Tabs(false, false);
                            tabs.ApplyTheme(Theme.Dark);
                            Check.Equal(Theme.Dark.GetStyle(Theme.TabFocusedRole), tabs.FocusedTabStyle!.Value, "role applied");

                            CellStyle custom = CellStyle.Default.WithAttribute(CellAttributes.Italic, true);
                            tabs.FocusedTabStyle = custom;
                            tabs.TabFocusMarker = string.Empty;
                            tabs.OnFocusChanged(true);
                            WidgetTester tester = WidgetTester.For(tabs, 30, 4).Render();
                            Check.Equal(" ", tester.CellAt(0, 0).Grapheme, "empty marker keeps the padding");
                            Check.Equal(custom, tester.CellAt(1, 0).Style, "explicit focused style used");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("TabFocus", "Guards", "Marker validation and an empty tab view",
                        _ =>
                        {
                            TabView tabs = new TabView();
                            Check.Throws<ArgumentNullException>(() => tabs.TabFocusMarker = null!, "null marker");
                            Check.Throws<ArgumentException>(() => tabs.TabFocusMarker = "->", "two-cell marker");
                            Check.Equal(">", tabs.TabFocusMarker, "default kept after rejected sets");

                            tabs.OnFocusChanged(true);
                            tabs.StripFocusStop = true;
                            tabs.ForwardKeys = true;
                            tabs.FocusEdge(true);
                            WidgetTester.For(tabs, 10, 3).Render().AssertNotContains(">");
                            Check.Equal(0, tabs.GetKeyHints()!.Count, "no hints without tabs");
                            Check.False(tabs.MoveFocus(true), "nothing to move into");
                            return Task.CompletedTask;
                        })
                });
        }

        private static TabView Tabs(bool forward, bool stripStop)
        {
            TabView tabs = new TabView { ForwardKeys = forward, StripFocusStop = stripStop };
            ListView<string> first = new ListView<string> { IsFocused = false };
            first.SetItems(new[] { "a", "b", "c" });
            ListView<string> second = new ListView<string> { IsFocused = false };
            second.SetItems(new[] { "d", "e", "f" });
            tabs.Add("One", first);
            tabs.Add("Two", second);
            return tabs;
        }

        private static IWidget ContentAt(TabView tabs, int index)
        {
            tabs.Activate(index);
            IWidget content = tabs.ActiveContent!;
            tabs.Activate(0);
            return content;
        }
    }
}

namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Testing;
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for tab focus markers and the standalone strip (1.5.0, B10): <see cref="TabStrip"/>,
    /// <see cref="TabView.TabPrefix"/>, <see cref="TabView.TabSuffix"/>, <see cref="TabView.TabFocusPrefix"/>,
    /// and <see cref="TabView.TabFocusSuffix"/>.
    /// </summary>
    public static class TabStripSuite
    {
        /// <summary>
        /// Builds the tab strip suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "TabStrip",
                displayName: "Tab Focus Markers and the Standalone Strip",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("TabStrip", "BracketMarkersKeepPositions", "With > < while focused and [ ] otherwise, tab positions are identical in both states",
                        _ =>
                        {
                            TabStrip strip = new TabStrip { TabPrefix = "[", TabSuffix = "]", TabFocusPrefix = ">", TabFocusSuffix = "<" };
                            strip.Add("One").Add("Two").Add("Three");
                            strip.ActiveIndex = 1;
                            string unfocused = Snapshot.RenderWidget(strip, 30, 1);
                            strip.OnFocusChanged(true);
                            string focused = Snapshot.RenderWidget(strip, 30, 1);
                            Check.Equal("[One] [Two] [Three]", unfocused, "unfocused brackets");
                            Check.Equal("[One] >Two< [Three]", focused, "focused markers wrap the active tab");
                            Check.Equal(unfocused.IndexOf("Three", StringComparison.Ordinal), focused.IndexOf("Three", StringComparison.Ordinal), "later tabs do not move");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("TabStrip", "TabViewDefaultsMatch14", "TabView keeps its 1.4.0 strip: \" Name \" unfocused, \">Name \" focused, and \" Name \" with an empty marker",
                        _ =>
                        {
                            TabView tabs = new TabView();
                            tabs.Add("Alpha", new ToggleLeaf("a")).Add("Beta", new ToggleLeaf("b"));
                            Check.Equal(" Alpha   Beta", Snapshot.RenderWidget(tabs, 20, 2).Split('\n')[0], "unfocused strip");
                            tabs.OnFocusChanged(true);
                            Check.Equal(">Alpha   Beta", Snapshot.RenderWidget(tabs, 20, 2).Split('\n')[0], "focused strip");
                            tabs.TabFocusMarker = string.Empty;
                            Check.Equal(" Alpha   Beta", Snapshot.RenderWidget(tabs, 20, 2).Split('\n')[0], "empty marker falls back to the plain prefix");
                            Check.Equal(string.Empty, tabs.TabFocusPrefix, "the marker is the focus prefix");
                            tabs.TabFocusPrefix = "*";
                            Check.Equal("*", tabs.TabFocusMarker, "and back");
                            Check.Equal(" ", tabs.TabFocusSuffix, "focus suffix defaults to a space");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("TabStrip", "StandaloneKeysAndClicks", "A standalone strip switches with Left and Right, raises ActiveTabChanged, leaves Down unconsumed, and activates on click",
                        _ =>
                        {
                            TabStrip strip = new TabStrip().Add("One").Add("Two").Add("Three");
                            List<int> changes = new List<int>();
                            strip.ActiveTabChanged += (sender, args) => changes.Add(args.NewValue);
                            strip.OnFocusChanged(true);
                            WidgetTester tester = WidgetTester.For(strip, 30, 1).Render();
                            tester.Press(KeyCode.Right);
                            Check.Equal(1, strip.ActiveIndex, "Right moved on");
                            tester.Press(KeyCode.Left).Press(KeyCode.Left);
                            Check.Equal(2, strip.ActiveIndex, "Left wraps");
                            Check.Equal(3, changes.Count, "ActiveTabChanged for each switch");
                            Check.False(strip.HandleKey(KeyEvent.Special(KeyCode.Down)), "Down leaves the strip to the container");
                            Check.False(strip.HandleKey(KeyEvent.Special(KeyCode.Tab)), "Tab is not consumed either");
                            tester.Render().Click(1, 0);
                            Check.Equal(0, strip.ActiveIndex, "a click activates the first tab");
                            Check.True(strip.GetKeyHints()!.Count == 1, "Left/Right hint while focused");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("TabStrip", "Guards", "Null markers and out-of-range indexes are rejected, and an empty strip is inert",
                        _ =>
                        {
                            TabStrip strip = new TabStrip();
                            Check.Equal(-1, strip.ActiveIndex, "no tabs: -1");
                            Check.False(strip.HandleKey(KeyEvent.Special(KeyCode.Right)), "no tabs: no switch");
                            Check.Throws<ArgumentNullException>(() => strip.TabPrefix = null!, "null prefix");
                            Check.Throws<ArgumentNullException>(() => strip.TabFocusSuffix = null!, "null focus suffix");
                            Check.Throws<ArgumentNullException>(() => strip.Add(null!), "null name");
                            Check.Throws<ArgumentOutOfRangeException>(() => strip.ActiveIndex = 0, "index 0 with no tabs");
                            strip.Add("A");
                            Check.Throws<ArgumentOutOfRangeException>(() => strip.ActiveIndex = 1, "index past the end");
                            Check.Throws<ArgumentOutOfRangeException>(() => strip.ActiveIndex = -1, "negative index");
                            Check.Throws<ArgumentNullException>(() => new TabView().TabPrefix = null!, "TabView null prefix");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}

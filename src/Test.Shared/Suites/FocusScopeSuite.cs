namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Hosting;
    using TUIKit.Input;
    using TUIKit.Layout;
    using TUIKit.Terminal;
    using TUIKit.Testing;
    using TUIKit.Widgets;

    /// <summary>
    /// Touchstone suite covering hierarchical focus (U1): key and mouse forwarding into TabView, SplitView,
    /// ScrollView, and Collapsible children, FocusScope traversal, and container-aware FocusManager.
    /// </summary>
    public static class FocusScopeSuite
    {
        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "FocusScope",
                displayName: "Hierarchical Focus and Forwarding",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("FocusScope", "ScopeTraversal", "FocusScope moves with Tab, bubbles at the edge, and skips disabled children",
                        _ =>
                        {
                            FocusScope scope = new FocusScope();
                            TextField a = scope.Add(new TextField());
                            TextField b = scope.Add(new TextField { IsEnabled = false });
                            TextField c = scope.Add(new TextField());
                            Check.True(a.IsFocused, "first child focused");
                            Check.True(scope.HandleKey(KeyEvent.Special(KeyCode.Tab)), "Tab moved");
                            Check.True(ReferenceEquals(c, scope.Focused), "disabled child skipped");
                            Check.True(c.IsFocused && !a.IsFocused, "focus-aware children follow");
                            Check.False(scope.HandleKey(KeyEvent.Special(KeyCode.Tab)), "Tab at the end bubbles");
                            Check.True(scope.HandleKey(KeyEvent.Special(KeyCode.Tab, KeyModifiers.Shift)), "Shift+Tab moves back");
                            Check.True(ReferenceEquals(a, scope.Focused), "back to first");
                            scope.Wrap = true;
                            scope.HandleKey(KeyEvent.Special(KeyCode.Tab, KeyModifiers.Shift));
                            Check.True(ReferenceEquals(c, scope.Focused), "wraps when Wrap is set");
                            Check.False(b.IsFocused, "disabled never focused");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusScope", "ScopeRoutesKeys", "FocusScope gives the focused child first refusal",
                        _ =>
                        {
                            FocusScope scope = new FocusScope();
                            TextField field = scope.Add(new TextField());
                            scope.HandleKey(KeyEvent.Char('x'));
                            Check.Equal("x", field.Value, "typed into the focused child");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusScope", "NestedScopes", "Nested scopes enter from either edge and bubble out",
                        _ =>
                        {
                            FocusScope outer = new FocusScope();
                            TextField first = outer.Add(new TextField());
                            FocusScope inner = outer.Add(new FocusScope());
                            TextField innerA = inner.Add(new TextField());
                            TextField innerB = inner.Add(new TextField());
                            TextField last = outer.Add(new TextField());

                            outer.HandleKey(KeyEvent.Special(KeyCode.Tab));
                            Check.True(ReferenceEquals(innerA, outer.FocusedLeaf), "entered inner at its first child");
                            outer.HandleKey(KeyEvent.Special(KeyCode.Tab));
                            Check.True(ReferenceEquals(innerB, outer.FocusedLeaf), "moved within inner");
                            outer.HandleKey(KeyEvent.Special(KeyCode.Tab));
                            Check.True(ReferenceEquals(last, outer.FocusedLeaf), "bubbled out to the next sibling");
                            outer.HandleKey(KeyEvent.Special(KeyCode.Tab, KeyModifiers.Shift));
                            Check.True(ReferenceEquals(innerB, outer.FocusedLeaf), "entered inner from its last child");
                            Check.False(first.IsFocused, "first lost focus");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusScope", "FocusManagerContainers", "FocusManager descends into containers and honors Wrap",
                        _ =>
                        {
                            FocusManager manager = new FocusManager();
                            FocusScope group = new FocusScope();
                            TextField g1 = group.Add(new TextField());
                            TextField g2 = group.Add(new TextField());
                            TextField after = new TextField();
                            manager.Register(group, after);
                            manager.HandleKey(KeyEvent.Special(KeyCode.Tab));
                            Check.True(ReferenceEquals(g2, group.Focused), "Tab moved inside the container first");
                            manager.HandleKey(KeyEvent.Special(KeyCode.Tab));
                            Check.Equal(1, manager.FocusedIndex, "then moved on");
                            manager.Wrap = false;
                            Check.False(manager.HandleKey(KeyEvent.Special(KeyCode.Tab)), "edge reported when not wrapping");
                            Check.True(g1 != null, "first exists");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusScope", "FocusManagerSkipsDisabled", "FocusManager skips disabled widgets",
                        _ =>
                        {
                            FocusManager manager = new FocusManager();
                            TextField a = new TextField();
                            Checkbox b = new Checkbox("b") { IsEnabled = false };
                            TextField c = new TextField();
                            manager.Register(a, b, c);
                            manager.Next();
                            Check.Equal(2, manager.FocusedIndex, "skipped the disabled checkbox");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusScope", "TabViewForwardsKeys", "TabView forwards keys to the active tab when ForwardKeys is set",
                        _ =>
                        {
                            TextField field = new TextField();
                            TabView tabs = new TabView { ForwardKeys = true };
                            tabs.Add("One", field).Add("Two", new Label(Text.From("two")));
                            tabs.OnFocusChanged(true);
                            Check.True(tabs.HandleKey(KeyEvent.Char('a')), "char consumed by child");
                            Check.Equal("a", field.Value, "child received the key");
                            Check.True(tabs.HandleKey(KeyEvent.Special(KeyCode.Left)), "Left consumed by the text field");
                            Check.Equal(0, tabs.ActiveIndex, "still on the first tab");
                            int changes = 0;
                            tabs.ActiveTabChanged += (s, e) => changes++;
                            Check.True(tabs.HandleKey(KeyEvent.Special(KeyCode.PageDown, KeyModifiers.Ctrl)), "Ctrl+PageDown switches");
                            Check.Equal(1, tabs.ActiveIndex, "second tab");
                            Check.Equal(1, changes, "change event raised");
                            Check.False(tabs.HandleKey(KeyEvent.Special(KeyCode.Tab)), "Tab bubbles out in forwarding mode");
                            Check.False(field.IsFocused, "hidden tab content lost focus");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusScope", "TabViewDefaultUnchanged", "TabView without ForwardKeys keeps Tab switching tabs",
                        _ =>
                        {
                            TextField field = new TextField();
                            TabView tabs = new TabView();
                            tabs.Add("One", field).Add("Two", new TextField());
                            Check.True(tabs.HandleKey(KeyEvent.Special(KeyCode.Tab)), "Tab switches");
                            Check.Equal(1, tabs.ActiveIndex, "switched");
                            tabs.HandleKey(KeyEvent.Char('z'));
                            Check.Equal(string.Empty, field.Value, "child never saw keys");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusScope", "TabViewForwardsMouse", "TabView forwards clicks below the strip to the content",
                        _ =>
                        {
                            RecordingMouseWidget content = new RecordingMouseWidget();
                            TabView tabs = new TabView();
                            tabs.Add("One", content);
                            Snapshot.RenderWidget(tabs, 20, 5);
                            tabs.HandleMouse(new MouseEvent(MouseEventKind.Press, MouseButton.Left, 3, 2, KeyModifiers.None, 1));
                            Check.True(content.Events.Count > 0, "content received the press");
                            Check.Equal(1, content.Events[content.Events.Count - 1].Y, "y translated below the strip");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusScope", "SplitViewForwardsKeys", "SplitView routes keys to the focused pane and Tab moves between panes",
                        _ =>
                        {
                            TextField left = new TextField();
                            TextField right = new TextField();
                            SplitView split = new SplitView(SplitOrientation.Horizontal, left, right) { ForwardKeys = true };
                            split.OnFocusChanged(true);
                            split.HandleKey(KeyEvent.Char('l'));
                            Check.Equal("l", left.Value, "left pane typed");
                            Check.True(split.HandleKey(KeyEvent.Special(KeyCode.Tab)), "Tab moves to the right pane");
                            Check.Equal(1, split.FocusedPane, "right pane focused");
                            split.HandleKey(KeyEvent.Char('r'));
                            Check.Equal("r", right.Value, "right pane typed");
                            Check.True(right.IsFocused && !left.IsFocused, "focus-aware panes follow");
                            Check.False(split.HandleKey(KeyEvent.Special(KeyCode.Tab)), "Tab bubbles from the last pane");
                            double ratio = split.Ratio;
                            split.HandleKey(KeyEvent.Special(KeyCode.Right, KeyModifiers.Ctrl | KeyModifiers.Shift));
                            Check.True(split.Ratio > ratio, "resize modifier resizes");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusScope", "SplitViewClickFocusesAndDrags", "Clicking a pane focuses it; dragging the divider resizes",
                        _ =>
                        {
                            TextField left = new TextField();
                            TextField right = new TextField();
                            SplitView split = new SplitView(SplitOrientation.Horizontal, left, right) { ForwardKeys = true };
                            Snapshot.RenderWidget(split, 21, 3);
                            split.HandleMouse(new MouseEvent(MouseEventKind.Press, MouseButton.Left, 15, 0, KeyModifiers.None, 1));
                            Check.Equal(1, split.FocusedPane, "click focused the right pane");
                            split.HandleMouse(new MouseEvent(MouseEventKind.Press, MouseButton.Left, 10, 0, KeyModifiers.None, 1));
                            split.HandleMouse(new MouseEvent(MouseEventKind.Move, MouseButton.Left, 15, 0, KeyModifiers.None, 0));
                            split.HandleMouse(new MouseEvent(MouseEventKind.Release, MouseButton.Left, 15, 0, KeyModifiers.None, 0));
                            Check.True(split.Ratio > 0.6, "divider drag moved the split");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusScope", "ScrollViewForwards", "ScrollView forwards keys first and mouse in content coordinates",
                        _ =>
                        {
                            ListView<string> list = new ListView<string>();
                            List<string> items = new List<string>();
                            for (int i = 0; i < 30; i++)
                                items.Add("item " + i);
                            list.SetItems(items);
                            ScrollView view = new ScrollView(list, 20, 30) { ForwardKeys = true };
                            view.HandleKey(KeyEvent.Special(KeyCode.Down));
                            Check.Equal(1, list.SelectedIndex, "Down went to the list");
                            Check.Equal(0, view.ScrollY, "view did not scroll for the consumed key");
                            Snapshot.RenderWidget(view, 20, 5);
                            view.ScrollTo(0, 10);
                            Snapshot.RenderWidget(view, 20, 5);
                            view.HandleMouse(new MouseEvent(MouseEventKind.Press, MouseButton.Left, 2, 1, KeyModifiers.None, 1));
                            Check.Equal(11, list.SelectedIndex, "click mapped through the scroll offset");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusScope", "CollapsibleScope", "Collapsible with ForwardKeys moves into its child and toggles from the header",
                        _ =>
                        {
                            TextField field = new TextField();
                            Collapsible section = new Collapsible("Advanced", field) { ForwardKeys = true };
                            section.OnFocusChanged(true);
                            bool? changed = null;
                            section.ExpandedChanged += (s, e) => changed = e.NewValue;
                            Check.True(section.HandleKey(KeyEvent.Special(KeyCode.Left)), "Left collapses from the header");
                            Check.False(section.Expanded, "collapsed");
                            Check.Equal(false, changed, "event raised");
                            section.HandleKey(KeyEvent.Special(KeyCode.Right));
                            Check.True(section.HandleKey(KeyEvent.Special(KeyCode.Tab)), "Tab enters the child");
                            Check.True(section.ChildFocused && field.IsFocused, "child focused");
                            section.HandleKey(KeyEvent.Char(' '));
                            Check.Equal(" ", field.Value, "Space goes to the child, not the toggle");
                            Check.True(section.Expanded, "still expanded");
                            Check.False(section.HandleKey(KeyEvent.Special(KeyCode.Tab)), "Tab bubbles out after the child");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusScope", "HostEntersContainer", "Host Tab traversal enters a container region at its first child",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 10);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                FocusScope scope = new FocusScope();
                                TextField a = scope.Add(new TextField());
                                TextField b = scope.Add(new TextField());
                                ScopeWidget widget = new ScopeWidget(scope);
                                app.Layout = Layout.Create()
                                    .Add("one", r => r.LeftAnchored(0, 20).FillHeight())
                                    .Add("two", r => r.RightAnchored(0, 20).FillHeight())
                                    .Build();
                                TextField other = new TextField();
                                app.Bind("one", other);
                                app.Bind("two", widget);
                                app.Start();
                                backend.FeedKey("tab");
                                app.PumpInputOnce();
                                Check.Equal("two", app.FocusedRegion, "moved to the container region");
                                Check.True(ReferenceEquals(a, scope.Focused), "entered at the first child");
                                backend.FeedKey("tab");
                                app.PumpInputOnce();
                                Check.True(ReferenceEquals(b, scope.Focused), "Tab moved inside the container");
                                backend.FeedKey("tab");
                                app.PumpInputOnce();
                                Check.Equal("one", app.FocusedRegion, "bubbled out to the next region");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        })
                });
        }
    }
}

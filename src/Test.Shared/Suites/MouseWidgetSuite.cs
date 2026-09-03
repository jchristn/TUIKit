namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Widgets;

    /// <summary>
    /// Touchstone suite covering widget-level mouse behavior: click-to-activate, wheel stepping and
    /// horizontal scrolling, and hover state discipline (hover never changes selection or focus, and
    /// Enter/Move/Leave are never consumed).
    /// </summary>
    public static class MouseWidgetSuite
    {
        /// <summary>
        /// Builds the widget mouse suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "MouseWidget",
                displayName: "Widget Mouse Behavior",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("MouseWidget", "TabViewClickActivates", "Clicking a tab header activates that tab",
                        _ =>
                        {
                            TabView tabs = new TabView();
                            tabs.Add("one", new Label(Text.From("1"))).Add("two", new Label(Text.From("2")));

                            // Headers: " one " at x 0-4, gap at 5, " two " at x 6-10.
                            Check.True(tabs.HandleMouse(Press(7, 0)), "Press on the second header consumed");
                            Check.Equal(1, tabs.ActiveIndex, "Second tab active");

                            Check.False(tabs.HandleMouse(Press(2, 3)), "Press below the strip not consumed");
                            Check.Equal(1, tabs.ActiveIndex, "Active tab unchanged");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseWidget", "TabViewHoverNeverActivates", "Hover motion is observed but never consumed or activating",
                        _ =>
                        {
                            TabView tabs = new TabView();
                            tabs.Add("one", new Label(Text.From("1"))).Add("two", new Label(Text.From("2")));

                            Check.False(tabs.HandleMouse(Motion(MouseEventKind.Enter, 7, 0)), "Enter unconsumed");
                            Check.False(tabs.HandleMouse(Motion(MouseEventKind.Move, 7, 0)), "Move unconsumed");
                            Check.Equal(0, tabs.ActiveIndex, "Hover did not activate");
                            Check.False(tabs.HandleMouse(Motion(MouseEventKind.Leave, 0, 0)), "Leave unconsumed");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseWidget", "CheckboxClickToggles", "A left press toggles; other buttons do not",
                        _ =>
                        {
                            Checkbox box = new Checkbox("opt");
                            Check.True(box.HandleMouse(Press(1, 0)), "Left press consumed");
                            Check.True(box.Checked, "Toggled on");
                            Check.True(box.HandleMouse(Press(1, 0)), "Second press consumed");
                            Check.False(box.Checked, "Toggled off");

                            Check.False(box.HandleMouse(new MouseEvent(MouseEventKind.Press, MouseButton.Right, 1, 0, KeyModifiers.None, 1)), "Right press ignored");
                            Check.False(box.Checked, "State unchanged by right press");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseWidget", "ListViewClickSelectsWheelSteps", "Click selects the row under the pointer; wheel steps the selection",
                        _ =>
                        {
                            ListView<string> list = new ListView<string>();
                            list.SetItems(new List<string> { "a", "b", "c", "d" });

                            Check.True(list.HandleMouse(Press(0, 2)), "Press on row 2 consumed");
                            Check.Equal(2, list.SelectedIndex, "Row 2 selected");

                            Check.True(list.HandleMouse(Wheel(MouseButton.WheelUp)), "Wheel up consumed");
                            Check.Equal(1, list.SelectedIndex, "Wheel stepped up");
                            Check.True(list.HandleMouse(Wheel(MouseButton.WheelDown)), "Wheel down consumed");
                            Check.Equal(2, list.SelectedIndex, "Wheel stepped down");

                            Check.False(list.HandleMouse(Motion(MouseEventKind.Move, 0, 0)), "Hover unconsumed");
                            Check.Equal(2, list.SelectedIndex, "Hover did not move the selection");

                            Check.False(list.HandleMouse(Press(0, 9)), "Press past the last row unconsumed");
                            Check.Equal(2, list.SelectedIndex, "Selection unchanged");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseWidget", "TreeClickSelectsAndToggles", "First click selects a node; a second click toggles expansion",
                        _ =>
                        {
                            Tree<string> tree = new Tree<string>(
                                "root",
                                node => node == "root" ? new List<string> { "a", "b" } : (node == "a" ? new List<string> { "a1" } : new List<string>()),
                                node => node);

                            // Visible rows: root (0), a (1), b (2).
                            Check.True(tree.HandleMouse(Press(0, 1)), "Press selects node a");
                            Check.Equal("a", tree.SelectedNode, "Node a selected");

                            Check.True(tree.HandleMouse(Press(0, 1)), "Second press toggles expansion");
                            Check.True(tree.IsExpanded("a"), "Node a expanded");

                            Check.True(tree.HandleMouse(Press(0, 1)), "Third press collapses");
                            Check.False(tree.IsExpanded("a"), "Node a collapsed");

                            Check.False(tree.HandleMouse(Press(0, 8)), "Press past the visible nodes unconsumed");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseWidget", "MenuBarClickDrivesMenus", "Title clicks open, item clicks activate, outside clicks dismiss",
                        _ =>
                        {
                            bool fired = false;
                            MenuBar bar = new MenuBar();
                            bar.AddMenu("File").Add("Open", () => fired = true).Add("Quit");
                            bar.AddMenu("Edit").Add("Copy");

                            // Titles: " File " at x 0-5, " Edit " at x 6-11.
                            Check.True(bar.HandleMouse(Press(1, 0)), "Press on File consumed");
                            Check.True(bar.IsOpen, "Drop-down open");
                            Check.Equal(0, bar.ActiveMenu, "File active");

                            // Render to establish the drop-down geometry used for item hit-testing.
                            CellBuffer buffer = new CellBuffer(40, 10);
                            bar.Render(new BufferSurface(buffer));

                            Check.False(bar.HandleMouse(Motion(MouseEventKind.Move, 2, 2)), "Hover over an item unconsumed");
                            Check.Equal(0, bar.HighlightedItem, "Hover highlights Open");

                            Check.True(bar.HandleMouse(Press(2, 2)), "Press on the first item consumed");
                            Check.True(fired, "Item action ran");
                            Check.False(bar.IsOpen, "Menu closed after activation");

                            Check.True(bar.HandleMouse(Press(8, 0)), "Press on Edit consumed");
                            Check.True(bar.IsOpen, "Edit drop-down open");
                            Check.Equal(1, bar.ActiveMenu, "Edit active");

                            bar.Render(new BufferSurface(new CellBuffer(40, 10)));
                            Check.True(bar.HandleMouse(Press(30, 8)), "Press outside dismisses");
                            Check.False(bar.IsOpen, "Menu closed");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseWidget", "ScrollViewHorizontalWheel", "WheelLeft/WheelRight scroll horizontally",
                        _ =>
                        {
                            ScrollView view = new ScrollView(new Label(Text.From("wide")), 200, 5);
                            Check.True(view.HandleMouse(Wheel(MouseButton.WheelRight)), "Wheel right consumed");
                            Check.Equal(3, view.ScrollX, "Scrolled right");
                            Check.True(view.HandleMouse(Wheel(MouseButton.WheelLeft)), "Wheel left consumed");
                            Check.Equal(0, view.ScrollX, "Scrolled back, clamped at zero");
                            Check.Equal(0, view.ScrollY, "Vertical offset untouched");
                            return Task.CompletedTask;
                        })
                });
        }

        private static MouseEvent Press(int x, int y)
        {
            return new MouseEvent(MouseEventKind.Press, MouseButton.Left, x, y, KeyModifiers.None, 1);
        }

        private static MouseEvent Motion(MouseEventKind kind, int x, int y)
        {
            return new MouseEvent(kind, MouseButton.None, x, y, KeyModifiers.None, 0);
        }

        private static MouseEvent Wheel(MouseButton button)
        {
            return new MouseEvent(MouseEventKind.Wheel, button, 0, 0, KeyModifiers.None, 0);
        }
    }
}

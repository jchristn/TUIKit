namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Hosting;
    using TUIKit.Input;
    using TUIKit.Layout;
    using TUIKit.Terminal;
    using TUIKit.Testing;
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for the framed stack container (1.5.0, B1): <see cref="FramedStack"/> and
    /// <see cref="StackSize"/>, including shared lines, the whole focused frame, nested grids, the
    /// overlay, mouse routing, narrow sizes, and focus audits on a headless host.
    /// </summary>
    public static class FramedStackSuite
    {
        private const string HeavyOnly = "━┃┏┓┗┛┣┫┳┻╋";

        /// <summary>
        /// Builds the framed stack suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "FramedStack",
                displayName: "Framed Stacks of Panes",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("FramedStack", "ThreePanesSharedLines", "Three vertical children render three boxes with two shared lines, and Tab focuses each in turn",
                        _ =>
                        {
                            ToggleLeaf a = new ToggleLeaf("a");
                            ToggleLeaf b = new ToggleLeaf("b");
                            ToggleLeaf c = new ToggleLeaf("c");
                            FramedStack stack = new FramedStack()
                                .Add(a, StackSize.Weighted(1), "A")
                                .Add(b, StackSize.Weighted(1), "B")
                                .Add(c, StackSize.Weighted(1), "C");
                            stack.OnFocusChanged(true);
                            WidgetTester tester = WidgetTester.For(stack, 20, 13).Render();
                            Check.Equal("┣", tester.CellAt(0, 4).Grapheme, "a's bottom-left joins b's line, heavy since a is focused");
                            Check.Equal("├", tester.CellAt(0, 8).Grapheme, "second shared line is light");
                            Check.Equal("└", tester.CellAt(0, 12).Grapheme, "c's frame closes light at the bottom");
                            Check.Equal("a", tester.Row(1).Substring(2, 1), "a's content inside its frame");
                            Check.Equal("b", tester.Row(5).Substring(2, 1), "b's content between the lines");
                            Check.Equal("c", tester.Row(9).Substring(2, 1), "c's content");
                            AssertOneHeavyOutline(tester, new[] { 0, 4, 8, 12 }, 0);

                            tester.Press(KeyEvent.Special(KeyCode.Tab)).Render();
                            Check.True(b.HasFocus && !a.HasFocus, "Tab moved to b");
                            AssertOneHeavyOutline(tester, new[] { 0, 4, 8, 12 }, 1);
                            tester.Press(KeyEvent.Special(KeyCode.Tab)).Render();
                            Check.True(c.HasFocus, "Tab moved to c");
                            AssertOneHeavyOutline(tester, new[] { 0, 4, 8, 12 }, 2);
                            Check.False(stack.HandleKey(KeyEvent.Special(KeyCode.Tab)), "Tab at the end leaves the stack");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FramedStack", "AuditOnHeadlessHost", "A stack bound to a headless host passes FocusAudit and shows one focused frame per stop",
                        _ =>
                        {
                            using (TuiApplication app = new TuiApplication(new HeadlessBackend(30, 13) { ClaimsTerminal = false }))
                            {
                                FramedStack stack = new FramedStack();
                                ListView<string> filter = new ListView<string>();
                                filter.SetItems(new[] { "all", "open" });
                                ListView<string> grid = new ListView<string>();
                                grid.SetItems(new[] { "row 1", "row 2", "row 3" });
                                ListView<string> detail = new ListView<string>();
                                detail.SetItems(new[] { "detail" });
                                stack.Add(filter, StackSize.Fixed(2), "Filter").Add(grid, StackSize.Weighted(1), "Grid").Add(detail, StackSize.Fixed(1), "Detail");
                                app.Layout = Layout.Create().Add("main", r => r.FillWidth().FillHeight().WithPadding(0)).Build();
                                app.Bind("main", stack);
                                app.Start();
                                FocusAuditResult result = FocusAudit.Run(app);
                                Check.True(result.IsClean, "audit clean: " + string.Join("; ", result.Problems.Select(p => p.ToString())));
                                Check.Equal(3, result.Stops, "three stops");
                                Check.True(app.CurrentFocusPath.Nodes.Contains(stack), "the stack is on the focus path");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FramedStack", "NestedGrid", "A horizontal stack inside a vertical one joins into a grid with tees and crosses",
                        _ =>
                        {
                            FramedStack top = new FramedStack(SplitOrientation.Horizontal)
                                .Add(new ToggleLeaf("l"), StackSize.Weighted(1))
                                .Add(new ToggleLeaf("r"), StackSize.Weighted(1));
                            FramedStack outer = new FramedStack()
                                .Add(top, StackSize.Weighted(1))
                                .Add(new ToggleLeaf("bottom"), StackSize.Weighted(1));
                            string[] rows = Snapshot.RenderWidget(outer, 21, 9).Split('\n');
                            Check.Equal("┌─────────┬─────────┐", rows[0], "top edge with a tee");
                            Check.Equal("├─────────┴─────────┤", rows[4], "middle line: tees at the sides, a tee up where the split ends");

                            FramedStack cross = new FramedStack()
                                .Add(new FramedStack(SplitOrientation.Horizontal).Add(new ToggleLeaf("a"), StackSize.Weighted(1)).Add(new ToggleLeaf("b"), StackSize.Weighted(1)), StackSize.Weighted(1))
                                .Add(new FramedStack(SplitOrientation.Horizontal).Add(new ToggleLeaf("c"), StackSize.Weighted(1)).Add(new ToggleLeaf("d"), StackSize.Weighted(1)), StackSize.Weighted(1));
                            rows = Snapshot.RenderWidget(cross, 21, 9).Split('\n');
                            Check.Equal("├─────────┼─────────┤", rows[4], "a two by two grid meets in a cross");
                            Check.Equal("└─────────┴─────────┘", rows[8], "bottom edge");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FramedStack", "OverlayTakesFocus", "An overlay takes focus and the focused frame, and closing it gives focus back",
                        _ =>
                        {
                            ToggleLeaf a = new ToggleLeaf("a");
                            ToggleLeaf b = new ToggleLeaf("b");
                            FramedStack stack = new FramedStack(SplitOrientation.Horizontal).Add(a, StackSize.Weighted(1)).Add(b, StackSize.Weighted(1));
                            stack.OnFocusChanged(true);
                            stack.Focus(b);
                            Check.True(b.HasFocus, "b focused");

                            ToggleLeaf drawer = new ToggleLeaf("drawer");
                            stack.OverlayRect = area => new Rect(area.X + 10, area.Y + 1, 10, 4);
                            stack.Overlay = drawer;
                            Check.True(drawer.HasFocus, "the overlay has focus");
                            Check.False(b.HasFocus, "b lost it");
                            Check.True(ReferenceEquals(drawer, stack.FocusedWidget), "FocusedWidget is the overlay");
                            Check.True(ReferenceEquals(drawer, stack.FocusedLeaf), "and the leaf");

                            WidgetTester tester = WidgetTester.For(stack, 24, 8).Render();
                            Check.Equal("┏", tester.CellAt(10, 1).Grapheme, "the overlay frame is the focused frame");
                            Check.Equal("┌", tester.CellAt(0, 0).Grapheme, "children behind draw plain");
                            Check.True(tester.Contains("drawer"), "overlay content drawn");
                            stack.HandleKey(KeyEvent.Special(KeyCode.Enter));
                            Check.Equal(1, drawer.KeyCount, "keys go to the overlay");
                            Check.Equal(0, b.KeyCount, "not to the child behind");

                            stack.Overlay = null;
                            Check.False(drawer.HasFocus, "overlay told it lost focus");
                            Check.True(b.HasFocus, "focus restored to b");

                            stack.Overlay = drawer;
                            tester.Render().Click(1, 4);
                            Check.True(stack.Overlay == null, "a click outside closes the overlay");
                            Check.True(a.HasFocus, "and focuses the child under it");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FramedStack", "ClickRoutesInContentCoordinates", "A click in the second child focuses it and arrives in its content coordinates",
                        _ =>
                        {
                            ListView<string> first = new ListView<string>();
                            first.SetItems(new[] { "f0", "f1" });
                            ListView<string> second = new ListView<string>();
                            second.SetItems(new[] { "s0", "s1", "s2" });
                            FramedStack stack = new FramedStack().Add(first, StackSize.Weighted(1)).Add(second, StackSize.Weighted(1));
                            stack.OnFocusChanged(true);
                            WidgetTester tester = WidgetTester.For(stack, 20, 11).Render();
                            Rect content = stack.ContentRectOf(second);
                            Check.Equal(new Rect(1, 6, 18, 4), content, "second child's content rect");

                            tester.Click(3, content.Y + 2);
                            Check.True(ReferenceEquals(second, stack.FocusedWidget), "the click focused the second child");
                            Check.Equal(2, second.SelectedIndex, "the click arrived at row 2 of the child");
                            tester.Click(0, 5);
                            Check.True(ReferenceEquals(first, stack.FocusedWidget), "a click on the shared line belongs to the child above it");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FramedStack", "NarrowDropsChildren", "At 80x24 with five fixed children, the ones without room are skipped and nothing overlaps",
                        _ =>
                        {
                            FramedStack stack = new FramedStack();
                            List<ToggleLeaf> leaves = new List<ToggleLeaf>();
                            for (int i = 0; i < 5; i++)
                            {
                                ToggleLeaf leaf = new ToggleLeaf("p" + i);
                                leaves.Add(leaf);
                                stack.Add(leaf, StackSize.Fixed(5), "P" + i);
                            }

                            stack.OnFocusChanged(true);
                            WidgetTester.For(stack, 80, 24).Render();
                            List<Rect> shown = new List<Rect>();
                            for (int i = 0; i < 5; i++)
                            {
                                Rect rect = stack.ContentRectOf(leaves[i]);
                                if (i < 3)
                                    Check.Equal(5, rect.Height, "child " + i + " keeps 5 rows");
                                else
                                    Check.True(rect.IsEmpty, "child " + i + " is skipped");
                                if (!rect.IsEmpty)
                                    shown.Add(rect);
                            }

                            for (int i = 0; i < shown.Count; i++)
                            {
                                for (int j = i + 1; j < shown.Count; j++)
                                    Check.True(shown[i].Intersect(shown[j]).IsEmpty, "content " + i + " and " + j + " do not overlap");
                            }

                            Check.True(stack.MoveFocus(true) && stack.MoveFocus(true), "Tab reaches the third child");
                            Check.False(stack.MoveFocus(true), "the skipped children are not focus stops");

                            stack.Focus(leaves[4]);
                            using (TuiApplication app = new TuiApplication(new HeadlessBackend(80, 24) { ClaimsTerminal = false }))
                            {
                                app.Layout = Layout.Create().Add("main", r => r.FillWidth().FillHeight().WithPadding(0)).Build();
                                app.Bind("main", stack);
                                app.Start();
                                app.RenderOnce();
                                app.RenderOnce();
                                Check.False(ReferenceEquals(leaves[4], stack.FocusedWidget), "focus repaired off the dropped child");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FramedStack", "StackSizeDistribution", "Weights share the space, fixed sizes hold, and rounding leftovers go to the last weighted child",
                        _ =>
                        {
                            ToggleLeaf one = new ToggleLeaf("one");
                            ToggleLeaf two = new ToggleLeaf("two");
                            FramedStack weighted = new FramedStack().Add(one, StackSize.Weighted(1)).Add(two, StackSize.Weighted(2));
                            WidgetTester.For(weighted, 10, 33).Render();
                            Check.Equal(10, weighted.ContentRectOf(one).Height, "weight 1 gets 10 of 30");
                            Check.Equal(20, weighted.ContentRectOf(two).Height, "weight 2 gets 20 of 30");

                            ToggleLeaf fixedLeaf = new ToggleLeaf("fixed");
                            ToggleLeaf rest = new ToggleLeaf("rest");
                            FramedStack mixed = new FramedStack().Add(fixedLeaf, StackSize.Fixed(5)).Add(rest, StackSize.Min(2));
                            for (int height = 10; height <= 30; height++)
                            {
                                WidgetTester.For(mixed, 10, height).Render();
                                Check.Equal(5, mixed.ContentRectOf(fixedLeaf).Height, "fixed child at height " + height);
                                Check.Equal(height - 8, mixed.ContentRectOf(rest).Height, "the rest at height " + height);
                            }

                            ToggleLeaf x = new ToggleLeaf("x");
                            ToggleLeaf y = new ToggleLeaf("y");
                            ToggleLeaf z = new ToggleLeaf("z");
                            FramedStack thirds = new FramedStack(SplitOrientation.Horizontal).Add(x, StackSize.Weighted(1)).Add(y, StackSize.Weighted(1)).Add(z, StackSize.Weighted(1));
                            WidgetTester.For(thirds, 14, 5).Render();
                            Check.Equal(3, thirds.ContentRectOf(x).Width, "first third");
                            Check.Equal(3, thirds.ContentRectOf(y).Width, "second third");
                            Check.Equal(4, thirds.ContentRectOf(z).Width, "the leftover column goes to the last weighted child");

                            ToggleLeaf small = new ToggleLeaf("small");
                            ToggleLeaf big = new ToggleLeaf("big");
                            FramedStack minimums = new FramedStack().Add(small, StackSize.Weighted(1, 6)).Add(big, StackSize.Weighted(9));
                            WidgetTester.For(minimums, 10, 23).Render();
                            Check.Equal(6, minimums.ContentRectOf(small).Height, "a minimum beats a small share");
                            Check.Equal(14, minimums.ContentRectOf(big).Height, "the rest goes to the other child");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FramedStack", "HintsPassThrough", "The focused child's key hints reach the resolver through the stack",
                        _ =>
                        {
                            HintLeaf leaf = new HintLeaf { Hints = new[] { new KeyHint("F5", "Refresh") } };
                            FramedStack stack = new FramedStack().Add(leaf, StackSize.Weighted(1));
                            using (TuiApplication app = new TuiApplication(new HeadlessBackend(20, 6) { ClaimsTerminal = false }))
                            {
                                app.Layout = Layout.Create().Add("main", r => r.FillWidth().FillHeight()).Build();
                                app.Bind("main", stack);
                                app.Start();
                                app.RenderOnce();
                                IReadOnlyList<KeyHint> hints = new KeyHintResolver().Resolve(app.CurrentFocusPath);
                                Check.True(hints.Any(h => h.Key == "F5"), "child hint resolved");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FramedStack", "Guards", "Null children and sizes, duplicates, invalid sizes, and empty stacks",
                        _ =>
                        {
                            FramedStack stack = new FramedStack();
                            Check.Throws<ArgumentNullException>(() => stack.Add(null!, StackSize.Weighted(1)), "null child");
                            Check.Throws<ArgumentNullException>(() => stack.Add(new ToggleLeaf("x"), null!), "null size");
                            ToggleLeaf leaf = new ToggleLeaf("leaf");
                            stack.Add(leaf, StackSize.Fixed(1));
                            Check.Throws<ArgumentException>(() => stack.Add(leaf, StackSize.Fixed(1)), "duplicate child");
                            Check.Throws<ArgumentException>(() => stack.Focus(new ToggleLeaf("stranger")), "focus a stranger");
                            Check.Throws<ArgumentException>(() => stack.Focus(new RecordingMouseWidget()), "focus a widget not in the stack");
                            Check.Throws<ArgumentOutOfRangeException>(() => StackSize.Fixed(0), "Fixed(0)");
                            Check.Throws<ArgumentOutOfRangeException>(() => StackSize.Fixed(10001), "Fixed(10001)");
                            Check.Throws<ArgumentOutOfRangeException>(() => StackSize.Weighted(0), "Weighted(0)");
                            Check.Throws<ArgumentOutOfRangeException>(() => StackSize.Weighted(1001), "Weighted(1001)");
                            Check.Throws<ArgumentOutOfRangeException>(() => StackSize.Weighted(1, 0), "Weighted min 0");
                            Check.Throws<ArgumentOutOfRangeException>(() => StackSize.Min(0), "Min(0)");
                            Check.Throws<ArgumentNullException>(() => stack.FrameOptions = null!, "null options");
                            Check.True(stack.Remove(leaf), "removed");
                            Check.False(stack.Remove(leaf), "already gone");

                            FramedStack empty = new FramedStack();
                            Check.Equal(string.Empty, Snapshot.RenderWidget(empty, 10, 4).Trim(), "an empty stack renders nothing");
                            Check.False(FocusScope.IsTabStop(empty), "an empty stack is not a focus stop");
                            Check.False(FocusScope.IsFocusable(empty), "nor focusable");
                            Check.False(empty.HandleKey(KeyEvent.Special(KeyCode.Tab)), "keys fall through");

                            FramedStack labels = new FramedStack().Add(new RecordingMouseWidget(), StackSize.Weighted(1));
                            Check.False(FocusScope.IsTabStop(labels), "a stack of non-focusable children is not a focus stop");
                            Check.Equal("Fixed(3)", StackSize.Fixed(3).ToString(), "ToString");
                            return Task.CompletedTask;
                        })
                });
        }

        private static void AssertOneHeavyOutline(WidgetTester tester, int[] lines, int focused)
        {
            for (int i = 0; i < lines.Length - 1; i++)
            {
                bool heavy = true;
                for (int y = lines[i]; y <= lines[i + 1]; y++)
                {
                    string glyph = tester.CellAt(0, y).Grapheme;
                    heavy &= glyph.Length == 1 && HeavyOnly.IndexOf(glyph, StringComparison.Ordinal) >= 0;
                }

                Check.Equal(i == focused, heavy, "child " + i + (i == focused ? " has" : " does not have") + " the heavy outline");
            }
        }
    }
}

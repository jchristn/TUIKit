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
    using N = TUIKit.Diagnostics.TuiKitTelemetryNames;

    /// <summary>
    /// Coverage for inline click regions in custom-rendered widgets: <see cref="ClickRegionMap{TAction}"/>,
    /// <see cref="InlineButton"/>, and <see cref="InlineButtonStyle"/>, directly, inside a scrolled
    /// <see cref="ScrollView"/>, and through a host with real routed clicks.
    /// </summary>
    public static class ClickRegionSuite
    {
        /// <summary>
        /// Builds the click region suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "ClickRegion",
                displayName: "Inline Click Regions",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("ClickRegion", "ClickInvokesRowAction", "A click on a row's second button raises that row's action",
                        _ =>
                        {
                            ActionRowsWidget rows = new ActionRowsWidget(4);
                            List<string> invoked = new List<string>();
                            rows.Map.Invoked += region => invoked.Add(region.Action);
                            WidgetTester tester = WidgetTester.For(rows, 40, 4).Render();
                            Check.Equal("r02 [Open] o [Delete] d", tester.Row(2), "row layout");
                            Check.Equal(8, rows.Map.Count, "two buttons per row recorded");

                            tester.Click(16, 2);
                            Check.True(tester.LastMouseHandled, "click consumed");
                            Check.Equal("2:delete", invoked[0], "second button on the third row");
                            Check.Equal("d", rows.Map.HitTest(16, 2)!.Key, "the region carries its key");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ClickRegion", "ScrolledContentResolves", "After scrolling, the same screen cell resolves to the newly visible row",
                        _ =>
                        {
                            ActionRowsWidget rows = new ActionRowsWidget(30);
                            List<string> invoked = new List<string>();
                            rows.Map.Invoked += region => invoked.Add(region.Action);
                            ScrollView view = new ScrollView(rows, 40, 30);
                            WidgetTester tester = WidgetTester.For(view, 41, 5).Render();

                            tester.Click(5, 0);
                            Check.Equal("0:open", invoked[invoked.Count - 1], "before scrolling");

                            view.ScrollBy(0, 5);
                            tester.Render();
                            tester.Click(5, 0);
                            Check.Equal("5:open", invoked[invoked.Count - 1], "after scrolling by five rows");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ClickRegion", "HostRoutedClick", "A click fed through the host lands on the right inline button",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 6);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                ActionRowsWidget rows = new ActionRowsWidget(3);
                                string? invoked = null;
                                rows.Map.Invoked += region => invoked = region.Action;
                                app.Layout = Layout.Create().Add("main", r => r.FillWidth().FillHeight().WithPadding(0, 1, 0, 0)).Build();
                                app.Bind("main", rows);
                                app.Start();
                                app.RenderOnce();

                                backend.FeedClick(17, 2);
                                app.PumpInputOnce();
                                Check.Equal("1:delete", invoked, "region offset handled by the host translating coordinates");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ClickRegion", "NarrowDropsWholeButtons", "A narrow surface drops whole buttons and records no region for them",
                        _ =>
                        {
                            ActionRowsWidget rows = new ActionRowsWidget(1);
                            WidgetTester tester = WidgetTester.For(rows, 18, 1).Render();
                            Check.Equal("r00 [Open] o", tester.Row(0), "second button dropped entirely");
                            Check.Equal(0, rows.LastDeleteWidth, "Draw reported nothing drawn");
                            Check.Equal(1, rows.Map.Count, "only the drawn button recorded");
                            Check.Equal(InlineButton.Measure("Delete", "d"), 10, "measure counts brackets, space, and key");
                            Check.Equal(InlineButton.Measure("Go", null), 4, "measure without a key");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ClickRegion", "HoverStyle", "Only the button under the pointer draws in the hover style",
                        _ =>
                        {
                            ActionRowsWidget rows = new ActionRowsWidget(2);
                            WidgetTester tester = WidgetTester.For(rows, 40, 2).Render();
                            tester.Move(5, 0).Render();
                            CellStyle hovered = tester.CellAt(5, 0).Style;
                            CellStyle other = tester.CellAt(5, 1).Style;
                            Check.True((hovered.Attributes & CellAttributes.Underline) != 0, "hovered button underlined");
                            Check.True((other.Attributes & CellAttributes.Underline) == 0, "other row's button not underlined");

                            tester.Mouse(new MouseEvent(MouseEventKind.Leave, MouseButton.None, 0, 0, KeyModifiers.None, 0)).Render();
                            Check.True((tester.CellAt(5, 0).Style.Attributes & CellAttributes.Underline) == 0, "leave clears the hover");
                            Check.Equal(-1, rows.Map.PointerX, "pointer forgotten on leave");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ClickRegion", "ThemedStyle", "InlineButtonStyle.FromTheme uses the theme's accent and hover role",
                        _ =>
                        {
                            InlineButtonStyle style = InlineButtonStyle.FromTheme(Theme.Dark);
                            Check.Equal(Theme.Dark.Accent.Foreground, style.Label.Foreground, "label uses the accent");
                            Check.True((style.Hover.Attributes & CellAttributes.Underline) != 0, "hover is underlined");
                            Check.Equal(Theme.Dark.Muted, style.Key, "key is muted");
                            Check.Throws<ArgumentNullException>(() => InlineButtonStyle.FromTheme(null!), "null theme");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ClickRegion", "Misses", "Gaps, other buttons, stale regions, and empty rects invoke nothing",
                        _ =>
                        {
                            ActionRowsWidget rows = new ActionRowsWidget(2);
                            int invoked = 0;
                            rows.Map.Invoked += _ => invoked++;
                            WidgetTester tester = WidgetTester.For(rows, 40, 2).Render();

                            tester.Click(12, 0);
                            Check.False(tester.LastMouseHandled, "the gap between buttons is not consumed");
                            tester.Click(16, 0, MouseButton.Right);
                            Check.False(tester.LastMouseHandled, "a right click is not an invocation");
                            Check.Equal(0, invoked, "nothing invoked");

                            rows.Map.Clear();
                            Check.True(rows.Map.HitTest(5, 0) == null, "no stale regions after Clear");
                            tester.Click(5, 0);
                            Check.Equal(0, invoked, "cleared regions cannot be clicked");

                            Check.True(rows.Map.Add(new Rect(0, 0, 0, 1), "x") == null, "empty rect ignored");
                            Check.Equal(0, rows.Map.Count, "nothing recorded");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ClickRegion", "Guards", "Null arguments are rejected",
                        _ =>
                        {
                            ClickRegionMap<int> map = new ClickRegionMap<int>();
                            BufferSurface surface = new BufferSurface(new CellBuffer(10, 1));
                            Check.Throws<ArgumentNullException>(() => InlineButton.Draw(surface, 0, 0, null!, "k", 1, map), "null label");
                            Check.Throws<ArgumentNullException>(() => InlineButton.Draw(surface, 0, 0, "Go", "k", 1, null!), "null map");
                            Check.Throws<ArgumentNullException>(() => InlineButton.Draw(null!, 0, 0, "Go", "k", 1, map), "null surface");
                            Check.Throws<ArgumentNullException>(() => InlineButton.Measure(null!, null), "null label to measure");
                            Check.Throws<ArgumentNullException>(() => map.HandleMouse(null!), "null mouse");
                            Check.Equal(0, InlineButton.Draw(surface, -1, 0, "Go", null, 1, map), "negative column draws nothing");
                            Check.Equal(0, InlineButton.Draw(surface, 0, 3, "Go", null, 1, map), "row past the surface draws nothing");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ClickRegion", "InvokedCounter", "Invocations are counted",
                        _ =>
                        {
                            using (TelemetryCapture capture = new TelemetryCapture())
                            {
                                ActionRowsWidget rows = new ActionRowsWidget(1);
                                WidgetTester tester = WidgetTester.For(rows, 40, 1).Render();
                                tester.Click(5, 0);
                                tester.Click(16, 0);
                                Check.Equal(2.0, capture.Sum(N.ClickRegionsInvoked), "two invocations");
                            }

                            return Task.CompletedTask;
                        })
                });
        }
    }
}

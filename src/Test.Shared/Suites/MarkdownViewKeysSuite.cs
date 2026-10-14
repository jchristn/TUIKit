namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Text;
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
    /// Coverage for 1.7 item V1: <see cref="MarkdownView"/> keyboard scrolling, opt-in focus, key hints, the scroll
    /// methods, immediate clamping, and region auto-focus that respects <see cref="IFocusStop"/>.
    /// </summary>
    public static class MarkdownViewKeysSuite
    {
        private const string _Id = "MarkdownViewKeys";

        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: _Id,
                displayName: "Markdown View Keys and Focus",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(_Id, "KeysMove", "Each scrolling key moves the expected rows at a known size",
                        _ =>
                        {
                            MarkdownView view = Long(20);
                            Snapshot.RenderWidget(view, 20, 5);
                            Check.True(view.HandleKey(KeyEvent.Special(KeyCode.Down)), "Down consumed");
                            Check.Equal(1, view.ScrollOffset, "Down moves one row");
                            Check.True(view.HandleKey(KeyEvent.Special(KeyCode.Up)), "Up consumed");
                            Check.Equal(0, view.ScrollOffset, "Up moves one row");
                            Check.True(view.HandleKey(KeyEvent.Special(KeyCode.PageDown)), "PageDown consumed");
                            Check.Equal(4, view.ScrollOffset, "PageDown moves the viewport height minus one");
                            Check.True(view.HandleKey(KeyEvent.Special(KeyCode.PageUp)), "PageUp consumed");
                            Check.Equal(0, view.ScrollOffset, "PageUp moves back");
                            Check.True(view.HandleKey(KeyEvent.Special(KeyCode.End)), "End consumed");
                            Check.Equal(15, view.ScrollOffset, "End shows the last page");
                            Check.True(view.HandleKey(KeyEvent.Special(KeyCode.Home)), "Home consumed");
                            Check.Equal(0, view.ScrollOffset, "Home returns to the top");
                            Check.True(view.HandleKey(KeyEvent.Special(KeyCode.Up)), "Up at the top of a tall document is still consumed");
                            Check.False(view.HandleKey(KeyEvent.Char('x')), "a letter is never consumed");
                            Check.False(view.HandleKey(KeyEvent.Special(KeyCode.Enter)), "Enter is never consumed");

                            MarkdownView tiny = Long(20);
                            Snapshot.RenderWidget(tiny, 20, 1);
                            tiny.HandleKey(KeyEvent.Special(KeyCode.PageDown));
                            Check.Equal(1, tiny.ScrollOffset, "a one-row viewport pages by at least one row");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "ShortDocumentKeepsKeys", "Keys on a document shorter than the viewport return false",
                        _ =>
                        {
                            MarkdownView view = new MarkdownView("- a\n- b");
                            Snapshot.RenderWidget(view, 20, 5);
                            KeyCode[] codes = new[] { KeyCode.Up, KeyCode.Down, KeyCode.PageUp, KeyCode.PageDown, KeyCode.Home, KeyCode.End, KeyCode.F3 };
                            foreach (KeyCode code in codes)
                                Check.False(view.HandleKey(KeyEvent.Special(code)), code + " is left to the parent");

                            Check.Equal(0, view.ScrollOffset, "nothing moved");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "FocusStop", "IsFocusStop false is skipped by Tab traversal, true is not, and SetFocus works either way",
                        _ =>
                        {
                            FocusScope scope = new FocusScope();
                            ListView<string> first = scope.Add(List(new[] { "a" }));
                            MarkdownView view = scope.Add(Long(20));
                            ListView<string> last = scope.Add(List(new[] { "b" }));
                            Check.False(view.IsFocusStop, "not a Tab stop by default");
                            Check.False(FocusScope.IsTabStop(view), "the scope agrees");
                            Check.True(FocusScope.IsFocusable(view), "but it is focusable");

                            scope.SetFocus(first);
                            scope.MoveFocus(true);
                            Check.True(ReferenceEquals(last, scope.Focused), "Tab skipped the view");

                            scope.SetFocus(view);
                            Check.True(ReferenceEquals(view, scope.Focused), "SetFocus reaches a non-stop view");
                            Check.True(view.IsFocused, "the view was told it has focus");

                            view.IsFocusStop = true;
                            scope.SetFocus(first);
                            scope.MoveFocus(true);
                            Check.True(ReferenceEquals(view, scope.Focused), "Tab stops on the view once it is a stop");
                            scope.MoveFocus(true);
                            Check.False(view.IsFocused, "losing focus is reported");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "KeyHints", "Hints appear only while focused and scrollable, and never when ShowKeyHints is false",
                        _ =>
                        {
                            MarkdownView view = Long(20);
                            Snapshot.RenderWidget(view, 20, 5);
                            Check.True(view.GetKeyHints() == null, "no hints while unfocused");

                            view.OnFocusChanged(true);
                            IReadOnlyList<KeyHint>? hints = view.GetKeyHints();
                            Check.True(hints != null, "hints while focused and scrollable");
                            Check.Equal("Up/Down Scroll|PgUp/PgDn Page|Home/End Top/Bottom", Join(hints!), "hint text");

                            view.SetSearch("item");
                            Check.Equal(4, view.GetKeyHints()!.Count, "F3 is described while a search is set");

                            view.ShowKeyHints = false;
                            Check.True(view.GetKeyHints() == null, "ShowKeyHints false always returns null");

                            MarkdownView shortView = new MarkdownView("- a");
                            Snapshot.RenderWidget(shortView, 20, 5);
                            shortView.OnFocusChanged(true);
                            Check.True(shortView.GetKeyHints() == null, "no hints when nothing scrolls");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "ScrollMethods", "ScrollBy, ScrollToTop, and ScrollToEnd clamp at both ends, and ViewportHeight tracks the render",
                        _ =>
                        {
                            MarkdownView view = Long(20);
                            Check.Equal(0, view.ViewportHeight, "0 before the first render");
                            Check.Equal(0, view.ContentHeight, "no layout before the first render");
                            Snapshot.RenderWidget(view, 20, 5);
                            Check.Equal(5, view.ViewportHeight, "last rendered height");
                            Check.Equal(20, view.ContentHeight, "content height after render");

                            view.ScrollBy(-5);
                            Check.Equal(0, view.ScrollOffset, "floored at 0");
                            view.ScrollBy(100);
                            Check.Equal(15, view.ScrollOffset, "capped at the last page");
                            view.ScrollBy(int.MaxValue);
                            Check.Equal(15, view.ScrollOffset, "no overflow");
                            view.ScrollBy(int.MinValue);
                            Check.Equal(0, view.ScrollOffset, "no underflow");
                            view.ScrollToEnd();
                            Check.Equal(15, view.ScrollOffset, "ScrollToEnd");
                            view.ScrollToTop();
                            Check.Equal(0, view.ScrollOffset, "ScrollToTop");

                            view.Markdown = "- changed";
                            Check.Equal(0, view.ContentHeight, "a new source drops the layout until the next render");
                            Check.Equal(1, view.RowsAt(20).Count, "RowsAt gives the height up front");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "ClampsAtOnce", "Wheel-down or an assignment past the end, then one wheel-up, moves the view",
                        _ =>
                        {
                            MarkdownView view = Long(20);
                            Snapshot.RenderWidget(view, 20, 5);
                            for (int i = 0; i < 20; i++)
                                view.HandleMouse(Wheel(MouseButton.WheelDown));

                            Check.Equal(15, view.ScrollOffset, "a burst of wheel-down stops at the end");
                            view.HandleMouse(Wheel(MouseButton.WheelUp));
                            Check.Equal(12, view.ScrollOffset, "the first wheel-up moves at once");

                            view.ScrollOffset = 500;
                            Check.Equal(15, view.ScrollOffset, "the setter clamps after a render");
                            view.HandleMouse(Wheel(MouseButton.WheelUp));
                            Check.Equal(12, view.ScrollOffset, "wheel-up after an assignment past the end");

                            MarkdownView fresh = Long(20);
                            fresh.ScrollOffset = 500;
                            Check.Equal(500, fresh.ScrollOffset, "before the first render the value is stored");
                            Snapshot.RenderWidget(fresh, 20, 5);
                            Check.Equal(15, fresh.ScrollOffset, "and the first render clamps it");
                            Check.Throws<ArgumentOutOfRangeException>(() => fresh.ScrollOffset = -1, "negative offset");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "RegionAutoFocus", "A non-stop MarkdownView region bound first does not take initial focus; a stop does",
                        _ =>
                        {
                            foreach (bool stop in new[] { false, true })
                            {
                                HeadlessBackend backend = new HeadlessBackend(40, 10);
                                using (TuiApplication app = new TuiApplication(backend))
                                {
                                    app.Layout = Layout.Create()
                                        .Add("help", r => r.LeftAnchored(0, 20).FillHeight().WithPadding(0))
                                        .Add("list", r => r.LeftAnchored(20, 20).FillHeight().WithPadding(0))
                                        .Build();
                                    MarkdownView help = Long(30);
                                    help.IsFocusStop = stop;
                                    app.Bind("help", help);
                                    app.Bind("list", List(new[] { "a", "b" }));
                                    app.Start();
                                    app.RenderOnce();
                                    Check.Equal(stop ? "help" : "list", app.FocusedRegion, "initial focus with IsFocusStop " + stop);
                                    app.Stop();
                                }
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "ClickFocusesWheelDoesNot", "A press on a region-bound view focuses it; a wheel event scrolls it without moving focus",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 10);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                app.Layout = Layout.Create()
                                    .Add("list", r => r.LeftAnchored(0, 20).FillHeight().WithPadding(0))
                                    .Add("help", r => r.LeftAnchored(20, 20).FillHeight().WithPadding(0))
                                    .Build();
                                MarkdownView help = Long(30);
                                app.Bind("list", List(new[] { "a", "b" }));
                                app.Bind("help", help);
                                app.Start();
                                app.RenderOnce();
                                Check.Equal("list", app.FocusedRegion, "the list starts focused");

                                backend.FeedWheel(25, 2, 1);
                                app.PumpInputOnce();
                                Check.True(help.ScrollOffset > 0, "the wheel scrolled the view");
                                Check.Equal("list", app.FocusedRegion, "the wheel did not move focus");

                                backend.FeedClick(25, 2);
                                app.PumpInputOnce();
                                Check.Equal("help", app.FocusedRegion, "a click focused the view");
                                Check.True(help.IsFocused, "the view knows it is focused");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),
                });
        }

        internal static MarkdownView Long(int items)
        {
            return new MarkdownView(Items(items));
        }

        internal static string Items(int count)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < count; i++)
                builder.Append("- item ").Append(i).Append('\n');

            return builder.ToString();
        }

        internal static MouseEvent Wheel(MouseButton button)
        {
            return new MouseEvent(MouseEventKind.Wheel, button, 0, 0, KeyModifiers.None, 0);
        }

        private static ListView<string> List(string[] items)
        {
            ListView<string> list = new ListView<string>();
            list.SetItems(items);
            return list;
        }

        private static string Join(IReadOnlyList<KeyHint> hints)
        {
            List<string> parts = new List<string>();
            foreach (KeyHint hint in hints)
                parts.Add(hint.ToString());

            return string.Join("|", parts);
        }
    }
}

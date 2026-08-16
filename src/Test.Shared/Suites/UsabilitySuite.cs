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
    using TUIKit.Modals;
    using TUIKit.Terminal;
    using TUIKit.Widgets;

    /// <summary>
    /// Touchstone suite covering the v0.4.0 interaction contract: the host-owned focus ring and its
    /// input-precedence chain, focused-widget first refusal, the wired sequence timeout, host mouse
    /// routing (click-to-focus and wheel), typed modals and the loop scheduler, the multi-key
    /// <c>Bind</c> fix, the layout-construction guard, and the dock/shell layout helpers.
    /// </summary>
    public static class UsabilitySuite
    {
        /// <summary>
        /// Builds the usability suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Usability",
                displayName: "Usability and Interaction Contract",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Usability", "FocusManagerVisualState", "FocusManager drives IFocusAware visual state",
                        _ =>
                        {
                            FocusManager manager = new FocusManager();
                            TextField a = new TextField();
                            TextField b = new TextField();
                            manager.Register(a, b);

                            Check.True(a.IsFocused, "first widget focused on register");
                            Check.False(b.IsFocused, "second widget not focused");

                            manager.Next();
                            Check.False(a.IsFocused, "a blurred after Next");
                            Check.True(b.IsFocused, "b focused after Next");

                            manager.Previous();
                            Check.True(a.IsFocused, "a refocused after Previous");
                            Check.False(b.IsFocused, "b blurred after Previous");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Usability", "HostFocusRing", "Bound focusable widgets form a host focus ring",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(30, 8);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                TextField a = new TextField();
                                TextField b = new TextField();
                                app.Layout = Layout.Create().DockTop("a", 1).Fill("b").Build();
                                app.Bind("a", a);
                                app.Bind("b", b);

                                Check.Equal("a", app.FocusedRegion, "first focusable auto-focused");
                                Check.True(a.IsFocused, "a focused");
                                Check.False(b.IsFocused, "b not focused");
                                Check.Equal("a", app.FocusContext, "FocusContext follows focus");

                                string? changed = null;
                                app.FocusChanged += r => changed = r;

                                Check.True(app.FocusNext(), "focus moved");
                                Check.Equal("b", app.FocusedRegion, "next focus is b");
                                Check.True(b.IsFocused, "b focused after next");
                                Check.False(a.IsFocused, "a blurred after next");
                                Check.Equal("b", changed, "FocusChanged raised with new region");

                                app.Focus("a");
                                Check.Equal("a", app.FocusedRegion, "Focus(id) moves focus back");

                                Check.Throws<InvalidOperationException>(() => app.Focus("missing"), "Focus rejects non-focusable region");
                                Check.Throws<ArgumentException>(() => app.Focus(""), "Focus rejects an empty region id");
                                Check.Throws<ArgumentException>(() => app.Focus(null!), "Focus rejects a null region id");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Usability", "TabTraversal", "Tab moves host focus when the widget does not consume it",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(30, 8);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                app.Layout = Layout.Create().DockTop("a", 1).Fill("b").Build();
                                app.Bind("a", new TextField());
                                app.Bind("b", new TextField());
                                app.Start();

                                Check.Equal("a", app.FocusedRegion, "starts on a");
                                backend.FeedInput("\t"); // Tab
                                app.PumpInputOnce();
                                Check.Equal("b", app.FocusedRegion, "Tab advanced focus to b");

                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Usability", "BindSequenceFires", "Bind parses and fires a space-separated two-key sequence",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(10, 3);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                bool fired = false;
                                app.Bind("ctrl+k ctrl+t", () => fired = true); // must not throw (was the documented, broken example)
                                app.Start();
                                backend.FeedInput(new byte[] { 0x0B, 0x14 }); // Ctrl+K, Ctrl+T
                                app.PumpInputOnce();
                                Check.True(fired, "two-key sequence fired");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Usability", "AbandonedSequenceNotSwallowed", "An abandoned sequence prefix does not swallow the next key",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(10, 3);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                bool sequenceFired = false;
                                List<KeyEvent> fellThrough = new List<KeyEvent>();
                                app.Bind("ctrl+k ctrl+t", () => sequenceFired = true);
                                app.KeyReceived += k => fellThrough.Add(k);
                                app.Start();

                                backend.FeedInput(new byte[] { 0x0B, (byte)'x' }); // Ctrl+K then 'x'
                                app.PumpInputOnce();

                                Check.False(sequenceFired, "sequence must not fire on a non-matching second key");
                                Check.Equal(1, fellThrough.Count, "the abandoned second key reaches KeyReceived");
                                Check.Equal('x', fellThrough[0].Rune, "and it is the key that was pressed, not swallowed");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Usability", "SequenceTimeoutReset", "ResetPending clears a pending sequence so it cannot complete",
                        _ =>
                        {
                            CommandRoutingTable table = new CommandRoutingTable();
                            table.RegisterSequence(KeyChord.Parse("ctrl+k"), KeyChord.Parse("ctrl+t"), "cmd");
                            CommandRouter router = new CommandRouter(table);

                            router.BeginPending(KeyChord.Parse("ctrl+k"));
                            Check.True(router.HasPending, "pending after BeginPending");
                            router.ResetPending();
                            Check.False(router.HasPending, "cleared after ResetPending");

                            CommandResolution stale = router.TryCompletePending(KeyChord.Parse("ctrl+t"));
                            Check.Equal(CommandResolutionStatus.None, stale.Status, "no completion after a reset");

                            router.BeginPending(KeyChord.Parse("ctrl+k"));
                            CommandResolution done = router.TryCompletePending(KeyChord.Parse("ctrl+t"));
                            Check.Equal(CommandResolutionStatus.Command, done.Status, "a fresh sequence still completes");
                            Check.Equal("cmd", done.CommandId, "with the bound command id");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Usability", "WidgetFirstRefusal", "A focused widget's key beats a colliding global sequence prefix",
                        _ =>
                        {
                            // Without a focused widget, the global Ctrl+K sequence prefix is live.
                            HeadlessBackend backendA = new HeadlessBackend(20, 5);
                            bool firedWithoutWidget = false;
                            using (TuiApplication app = new TuiApplication(backendA))
                            {
                                app.Bind("ctrl+k ctrl+t", () => firedWithoutWidget = true);
                                app.Start();
                                backendA.FeedInput(new byte[] { 0x0B, 0x14 });
                                app.PumpInputOnce();
                                app.Stop();
                            }

                            Check.True(firedWithoutWidget, "global sequence fires when no widget claims Ctrl+K");

                            // With a focused TextEditor (which uses Ctrl+K itself), the widget takes it first.
                            HeadlessBackend backendB = new HeadlessBackend(20, 5);
                            bool firedWithWidget = false;
                            using (TuiApplication app = new TuiApplication(backendB))
                            {
                                app.Layout = Layout.Create().Fill("editor").Build();
                                app.Bind("editor", new TextEditor());
                                app.Bind("ctrl+k ctrl+t", () => firedWithWidget = true);
                                app.Start();
                                backendB.FeedInput(new byte[] { 0x0B, 0x14 });
                                app.PumpInputOnce();
                                app.Stop();
                            }

                            Check.False(firedWithWidget, "the focused editor consumes Ctrl+K, so the global sequence never starts");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Usability", "ClickToFocus", "A mouse press focuses the widget under the pointer",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(30, 8);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                TextField top = new TextField();
                                TextField main = new TextField();
                                app.Layout = Layout.Create().DockTop("top", 3).Fill("main").Build();
                                app.Bind("top", top);
                                app.Bind("main", main);
                                app.Start();

                                Check.Equal("top", app.FocusedRegion, "top auto-focused");
                                app.RenderOnce(); // builds the per-frame hit-test map

                                backend.FeedInput("[<0;3;6M"); // left press at col 3, row 6 (1-based) -> inside "main"
                                app.PumpInputOnce();

                                Check.Equal("main", app.FocusedRegion, "clicking the lower region focuses it");
                                Check.True(main.IsFocused, "main widget shows focus");
                                Check.False(top.IsFocused, "top widget blurred");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Usability", "WheelScrollsPaneUnderPointer", "The wheel scrolls the pane under the pointer via IMouseAware",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(20, 6);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                Pane pane = app.AddPane("log", r => r.FillWidth().FillHeight().WithPadding(0));
                                for (int i = 0; i < 40; i++)
                                    pane.WriteLine("line " + i);

                                app.Start();
                                app.RenderOnce();
                                Check.True(pane.IsAtBottom, "pane starts attached to the bottom");

                                backend.FeedInput("[<64;2;2M"); // SGR wheel-up at col 2, row 2
                                app.PumpInputOnce();

                                Check.False(pane.IsAtBottom, "wheel-up detached the pane from the bottom");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Usability", "TypedModal", "ShowAsync<T> returns the typed modal result",
                        async _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 10);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                MessageModal modal = new MessageModal("Pick", "Choose one", new[] { "OK", "Cancel" });
                                Task<int> result = app.ShowAsync<int>(modal);
                                modal.HandleKey(KeyEvent.Special(KeyCode.Enter)); // selects index 0
                                int index = await result.ConfigureAwait(false);
                                Check.Equal(0, index, "typed result is the chosen index");
                            }
                        }),

                    new TestCaseDescriptor("Usability", "PostRunsOnLoop", "Post drains onto the loop thread each frame",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(10, 3);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                bool ran = false;
                                app.Post(() => ran = true);
                                Check.False(ran, "posted action does not run until the loop drains it");
                                app.PumpInputOnce();
                                Check.True(ran, "posted action ran on the next pump");

                                Check.Throws<ArgumentNullException>(() => app.Post(null!), "Post rejects null");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Usability", "LayoutMixGuard", "Assigning Layout after incremental regions is rejected",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(20, 6);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                app.AddPane("a", r => r.FillWidth().FillHeight().WithPadding(0));
                                Check.Throws<InvalidOperationException>(
                                    () => app.Layout = Layout.Create().Add("b", r => r.FillWidth().FillHeight()).Build(),
                                    "assigning Layout after AddPane throws instead of discarding regions");
                            }

                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                app.Layout = Layout.Create().Add("b", r => r.FillWidth().FillHeight()).Build();
                                app.AddRegion("c", r => r.FillWidth().BottomAnchored(0, 1).WithPadding(0)); // append is fine
                                Check.Equal(2, app.Layout!.Regions.Count, "AddRegion appends to an assigned layout");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Usability", "DockShellLayout", "Dock helpers build a non-overlapping application shell",
                        _ =>
                        {
                            Layout layout = Layout.Create()
                                .DockTop("top", 1)
                                .DockBottom("bottom", 1)
                                .DockLeft("side", 10)
                                .Fill("main")
                                .Build();

                            Size size = new Size(40, 10);
                            Check.Equal(new Rect(0, 0, 40, 1), layout.FindById("top")!.Resolve(size), "top bar spans the width");
                            Check.Equal(new Rect(0, 9, 40, 1), layout.FindById("bottom")!.Resolve(size), "bottom bar hugs the last row");
                            Check.Equal(new Rect(0, 1, 10, 8), layout.FindById("side")!.Resolve(size), "sidebar fills between the bars");
                            Check.Equal(new Rect(10, 1, 30, 8), layout.FindById("main")!.Resolve(size), "main fills the remainder");
                            Check.False(layout.HasOverlap(size), "no two regions overlap");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}

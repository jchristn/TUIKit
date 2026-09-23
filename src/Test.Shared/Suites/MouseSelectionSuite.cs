namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Hosting;
    using TUIKit.Input;
    using TUIKit.Layout;
    using TUIKit.Modals;
    using TUIKit.Terminal;
    using TUIKit.Widgets;

    /// <summary>
    /// Touchstone suite covering the built-in, application-wide mouse text-selection layer on
    /// <see cref="TuiApplication"/>: click-drag selection clamped to the anchor region, replacement by a
    /// drag in another region, copy-on-Ctrl+C over the composited buffer, wide-glyph extraction, and the
    /// gates (feature flag, mouse capture, modal) that keep it inert.
    /// </summary>
    public static class MouseSelectionSuite
    {
        private const string Esc = "";

        /// <summary>
        /// Builds the mouse selection suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "MouseSelection",
                displayName: "Mouse Text Selection",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("MouseSelection", "SingleLineDragSelects", "A left click-drag selects a substring within a row",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 5);
                            using (TuiApplication app = BuildApp(backend, "hello world"))
                            {
                                app.Start();
                                app.RenderOnce();

                                Pump(app, backend, Press(0, 0));
                                Pump(app, backend, Drag(4, 0));
                                Pump(app, backend, Release(4, 0));

                                Check.True(app.HasTextSelection, "A selection exists after the drag");
                                Check.Equal("hello", app.GetSelectedText(), "Columns 0-4 selected");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseSelection", "ClampsToRegionRectangle", "Dragging past the region edge clamps to that region",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 5);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                app.Layout = Layout.Create()
                                    .Add("a", r => r.LeftAnchored(0, 20).FillHeight().WithPadding(0))
                                    .Add("b", r => r.RightAnchored(0, 20).FillHeight().WithPadding(0))
                                    .Build();
                                app.Bind("a", new Label(Text.From("aaaaaaaaaa")));
                                app.Bind("b", new Label(Text.From("bbbbbbbbbb")));
                                app.MouseTextSelectionEnabled = true;
                                app.Start();
                                app.RenderOnce();

                                // Press in region A, drag well into region B.
                                Pump(app, backend, Press(0, 0));
                                Pump(app, backend, Drag(30, 0));
                                Pump(app, backend, Release(30, 0));

                                string selected = app.GetSelectedText();
                                Check.Equal("aaaaaaaaaa", selected, "Selection clamps to region A's content");
                                Check.False(selected.Contains("b"), "No cells from region B leak in");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseSelection", "NewDragReplacesSelection", "A drag in another region discards the prior selection",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 5);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                app.Layout = Layout.Create()
                                    .Add("a", r => r.LeftAnchored(0, 20).FillHeight().WithPadding(0))
                                    .Add("b", r => r.RightAnchored(0, 20).FillHeight().WithPadding(0))
                                    .Build();
                                app.Bind("a", new Label(Text.From("aaaaaaaaaa")));
                                app.Bind("b", new Label(Text.From("bbbbbbbbbb")));
                                app.MouseTextSelectionEnabled = true;
                                app.Start();
                                app.RenderOnce();

                                Pump(app, backend, Press(0, 0));
                                Pump(app, backend, Drag(9, 0));
                                Pump(app, backend, Release(9, 0));
                                Check.Equal("aaaaaaaaaa", app.GetSelectedText(), "First selection is region A");

                                // Region B spans absolute columns 20-39; select its first five cells.
                                Pump(app, backend, Press(20, 0));
                                Pump(app, backend, Drag(24, 0));
                                Pump(app, backend, Release(24, 0));

                                string selected = app.GetSelectedText();
                                Check.Equal("bbbbb", selected, "Selection now lives in region B");
                                Check.False(selected.Contains("a"), "None of region A remains");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseSelection", "MultiRowFlowSelection", "A selection spanning rows flows in reading order within the region",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 5);
                            List<string> lines = new List<string> { "abcdef", "ghijkl", "mnopqr" };
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                app.Layout = Layout.Create()
                                    .Add("main", r => r.FillWidth().FillHeight().WithPadding(0))
                                    .Build();
                                app.Bind("main", new TextBlockProbeWidget(lines));
                                app.MouseTextSelectionEnabled = true;
                                app.Start();
                                app.RenderOnce();

                                // Anchor mid-first-row, focus mid-third-row: first partial row, full middle
                                // row, last partial row - trailing blanks trimmed per row.
                                Pump(app, backend, Press(3, 0));
                                Pump(app, backend, Drag(2, 2));
                                Pump(app, backend, Release(2, 2));

                                Check.Equal("def\nghijkl\nmno", app.GetSelectedText(), "Flow selection across three rows");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseSelection", "CtrlCCopiesAndClears", "Ctrl+C copies the selection over OSC 52 and clears it without invoking the policy",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 5);
                            using (TuiApplication app = BuildApp(backend, "hello world"))
                            {
                                app.CtrlCPolicy = CtrlCPolicy.InterruptFocusedPane;
                                bool interrupted = false;
                                app.Interrupted += () => interrupted = true;
                                string copied = string.Empty;
                                app.TextCopied += text => copied = text;

                                app.Start();
                                app.RenderOnce();

                                Pump(app, backend, Press(0, 0));
                                Pump(app, backend, Drag(4, 0));
                                Pump(app, backend, Release(4, 0));
                                Check.True(app.HasTextSelection, "Selection present before copy");

                                backend.TakeOutput();
                                backend.FeedInput(new byte[] { 0x03 });
                                app.PumpInputOnce();

                                string output = backend.TakeOutput();
                                string expectedPayload = Convert.ToBase64String(Encoding.UTF8.GetBytes("hello"));
                                Check.True(output.Contains("]52;c;" + expectedPayload), "OSC 52 carries the base64 of the selection");
                                Check.Equal("hello", copied, "TextCopied fired with the copied text");
                                Check.False(app.HasTextSelection, "Selection cleared after copy");
                                Check.False(interrupted, "The copy short-circuits the Ctrl+C policy");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseSelection", "CtrlCWithoutSelectionUsesPolicy", "With no selection Ctrl+C follows the existing policy",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 5);
                            using (TuiApplication app = BuildApp(backend, "hello world"))
                            {
                                app.CtrlCPolicy = CtrlCPolicy.InterruptFocusedPane;
                                bool interrupted = false;
                                app.Interrupted += () => interrupted = true;

                                app.Start();
                                app.RenderOnce();

                                backend.TakeOutput();
                                backend.FeedInput(new byte[] { 0x03 });
                                app.PumpInputOnce();

                                Check.True(interrupted, "The Ctrl+C policy ran because nothing was selected");
                                Check.False(backend.TakeOutput().Contains("]52;c;"), "No clipboard write without a selection");
                                Check.False(app.HasTextSelection, "Still nothing selected");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseSelection", "DisabledWhenSelectionOff", "A drag produces no selection while the feature is off",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 5);
                            using (TuiApplication app = BuildApp(backend, "hello world"))
                            {
                                app.MouseTextSelectionEnabled = false;
                                app.Start();
                                app.RenderOnce();

                                Pump(app, backend, Press(0, 0));
                                Pump(app, backend, Drag(4, 0));
                                Pump(app, backend, Release(4, 0));

                                Check.False(app.HasTextSelection, "No selection while disabled");
                                Check.Equal(string.Empty, app.GetSelectedText(), "Nothing to extract");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseSelection", "InertWhenMouseCaptureOff", "A drag produces no selection while mouse capture is off",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 5);
                            using (TuiApplication app = BuildApp(backend, "hello world"))
                            {
                                app.MouseCaptureEnabled = false;
                                app.Start();
                                app.RenderOnce();

                                Pump(app, backend, Press(0, 0));
                                Pump(app, backend, Drag(4, 0));
                                Pump(app, backend, Release(4, 0));

                                Check.False(app.HasTextSelection, "Inert when the terminal owns selection");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseSelection", "SuppressedWhileModalActive", "A drag does not select while a modal is active",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 5);
                            using (TuiApplication app = BuildApp(backend, "hello world"))
                            {
                                app.Start();
                                app.RenderOnce();
                                app.Modals.Push(new MessageModal("Title", "body", new List<string> { "OK" }));

                                Pump(app, backend, Press(0, 0));
                                Pump(app, backend, Drag(4, 0));
                                Pump(app, backend, Release(4, 0));

                                Check.False(app.HasTextSelection, "Modal owns the mouse; no selection");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseSelection", "PlainClickClearsSelection", "A click with no drag clears the previous selection",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 5);
                            using (TuiApplication app = BuildApp(backend, "hello world"))
                            {
                                app.Start();
                                app.RenderOnce();

                                Pump(app, backend, Press(0, 0));
                                Pump(app, backend, Drag(4, 0));
                                Pump(app, backend, Release(4, 0));
                                Check.True(app.HasTextSelection, "Selection made");

                                Pump(app, backend, Press(2, 0));
                                Pump(app, backend, Release(2, 0));
                                Check.False(app.HasTextSelection, "Plain click cleared it");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseSelection", "WideGlyphExtraction", "Wide glyphs extract without doubling or dropping columns",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 5);
                            // Two CJK glyphs then ASCII: cells are wide-lead + continuation + wide-lead + continuation + a b.
                            using (TuiApplication app = BuildApp(backend, "你好ab"))
                            {
                                app.Start();
                                app.RenderOnce();

                                Pump(app, backend, Press(0, 0));
                                Pump(app, backend, Drag(5, 0));
                                Pump(app, backend, Release(5, 0));

                                Check.Equal("你好ab", app.GetSelectedText(), "Both wide glyphs and the ASCII tail copy once each");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseSelection", "ClearsOnResize", "A resize drops a screen-cell selection",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 5);
                            using (TuiApplication app = BuildApp(backend, "hello world"))
                            {
                                app.Start();
                                app.RenderOnce();

                                Pump(app, backend, Press(0, 0));
                                Pump(app, backend, Drag(4, 0));
                                Pump(app, backend, Release(4, 0));
                                Check.True(app.HasTextSelection, "Selection made");

                                backend.Resize(30, 8);
                                app.RenderOnce();

                                Check.False(app.HasTextSelection, "Resize cleared the selection");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseSelection", "WheelClearsSelection", "Scrolling clears a screen-cell selection",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 5);
                            using (TuiApplication app = BuildApp(backend, "hello world"))
                            {
                                app.Start();
                                app.RenderOnce();

                                Pump(app, backend, Press(0, 0));
                                Pump(app, backend, Drag(4, 0));
                                Pump(app, backend, Release(4, 0));
                                Check.True(app.HasTextSelection, "Selection made");

                                Pump(app, backend, Esc + "[<64;3;1M"); // wheel up
                                Check.False(app.HasTextSelection, "Wheel cleared the selection");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        })
                });
        }

        private static TuiApplication BuildApp(HeadlessBackend backend, string text)
        {
            TuiApplication app = new TuiApplication(backend);
            app.Layout = Layout.Create()
                .Add("main", r => r.FillWidth().FillHeight().WithPadding(0))
                .Build();
            app.Bind("main", new Label(Text.From(text)));
            app.MouseTextSelectionEnabled = true;
            return app;
        }

        private static void Pump(TuiApplication app, HeadlessBackend backend, string sequence)
        {
            backend.FeedInput(sequence);
            app.PumpInputOnce();
        }

        private static string Press(int x, int y)
        {
            return Esc + "[<0;" + (x + 1) + ";" + (y + 1) + "M";
        }

        private static string Drag(int x, int y)
        {
            return Esc + "[<32;" + (x + 1) + ";" + (y + 1) + "M";
        }

        private static string Release(int x, int y)
        {
            return Esc + "[<0;" + (x + 1) + ";" + (y + 1) + "m";
        }
    }
}

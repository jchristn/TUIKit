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
    using TUIKit.Testing;
    using TUIKit.Unicode;
    using TUIKit.Widgets;

    /// <summary>
    /// Touchstone suite covering incremental streaming Markdown (U7), the Pane wrap cache and idle frame
    /// throttling (U8), cell-width text fitting (U9), the command palette and key-help overlay (U10),
    /// multi-series and stacked charts, programmatic modal closes, and key encoding for headless tests.
    /// </summary>
    public static class UpstreamRenderingSuite
    {
        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "UpstreamRendering",
                displayName: "Streaming, Performance, Width, Palette, Charts, Host Gaps",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("UpstreamRendering", "StreamingKeepsStructure", "Streaming Markdown keeps line structure before finalizing",
                        _ =>
                        {
                            Pane pane = new Pane("t");
                            StreamingTranscript transcript = new StreamingTranscript(pane);
                            transcript.AppendText("# Head");
                            transcript.AppendText("ing\n- one\n- tw");
                            IReadOnlyList<string> lines = pane.SnapshotPlainLines();
                            Check.Equal(3, lines.Count, "heading, bullet, live partial bullet");
                            Check.Equal("Heading", lines[0], "heading rendered without the marker");
                            Check.Equal("\u2022 one", lines[1], "bullet rendered");
                            Check.Equal("\u2022 tw", lines[2], "partial line rendered live");
                            transcript.AppendText("o\n```\ncode");
                            lines = pane.SnapshotPlainLines();
                            Check.Equal("\u2022 two", lines[2], "live line committed in place");
                            Check.Equal("  code", lines[lines.Count - 1], "fence removed, code styled while streaming");
                            transcript.AppendText("\n```");
                            transcript.FinalizeBlock();
                            List<string> expected = new List<string>();
                            foreach (StyledText line in MarkdownRenderer.Render("# Heading\n- one\n- two\n```\ncode\n```"))
                                expected.Add(line.ToPlainString());
                            Check.Equal(string.Join("|", expected), string.Join("|", pane.SnapshotPlainLines()), "final output equals a full render");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("UpstreamRendering", "StreamingLegacyMode", "RenderMarkdownWhileStreaming off keeps the single collapsed live line",
                        _ =>
                        {
                            Pane pane = new Pane("t");
                            StreamingTranscript transcript = new StreamingTranscript(pane) { RenderMarkdownWhileStreaming = false };
                            transcript.AppendText("a\nb");
                            Check.Equal("a b", pane.SnapshotPlainLines()[0], "collapsed");
                            transcript.FinalizeBlock();
                            Check.Equal(2, pane.LineCount, "rendered on finalize");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("UpstreamRendering", "PaneWrapCache", "Pane renders long scrollback correctly with the wrap cache and line updates",
                        _ =>
                        {
                            Pane pane = new Pane("p");
                            for (int i = 0; i < 200; i++)
                                pane.WriteLine("line " + i + " with enough text to wrap");
                            PaneLineHandle handle = pane.WriteLine("status: running");
                            string first = Snapshot.RenderWidget(pane, 20, 4);
                            Check.True(first.Contains("status: running"), "bottom shown");
                            handle.Update("status: done");
                            string second = Snapshot.RenderWidget(pane, 20, 4);
                            Check.True(second.Contains("status: done") && !second.Contains("running"), "cache invalidated by the update");
                            string narrow = Snapshot.RenderWidget(pane, 10, 4);
                            Check.True(narrow.Contains("done"), "rewrapped at a new width");
                            Check.True(handle.Remove(), "removed");
                            Check.False(Snapshot.RenderWidget(pane, 20, 4).Contains("status"), "line gone");
                            pane.ScrollUp(5);
                            string scrolled = Snapshot.RenderWidget(pane, 20, 4);
                            Check.True(scrolled.Contains("line"), "scrolled window materialized");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("UpstreamRendering", "IdleThrottle", "IdleFps validates and the session reports idle after quiet time",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(20, 5);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                Check.False(app.IsIdle, "throttling off by default");
                                Check.Throws<ArgumentOutOfRangeException>(() => app.IdleFps = -1, "negative");
                                app.IdleFps = 2;
                                app.IdleAfterMilliseconds = 0;
                                Check.True(app.IsIdle, "idle with a zero delay");
                                app.RequestRender();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("UpstreamRendering", "TextFit", "TextFit measures, truncates, pads, and slices by cell width",
                        _ =>
                        {
                            string cjk = "\u4E2D\u6587\u5B57";
                            Check.Equal(6, TextFit.Width(cjk), "two columns per glyph");
                            Check.Equal("\u4E2D\u6587", TextFit.Truncate(cjk, 5), "never splits a wide glyph");
                            Check.Equal("\u4E2D\u2026", TextFit.Ellipsize(cjk, 4), "ellipsis fits");
                            Check.Equal("ab   ", TextFit.PadRight("ab", 5), "pad right");
                            Check.Equal("   ab", TextFit.PadLeft("ab", 5), "pad left");
                            Check.Equal(" ab  ", TextFit.Center("ab", 5), "center");
                            Check.Equal(" \u6587", TextFit.Slice(cjk, 1, 3), "slice replaces a cut glyph with a space");
                            Check.Equal(4, TextFit.ColumnOf(cjk, 2), "column of an index");
                            Check.Equal(1, TextFit.IndexAtColumn(cjk, 3), "index at a column inside a glyph");
                            Check.Equal(2, TextFit.NextBoundary("\U0001F600x", 0), "surrogate pair boundary");
                            Check.Equal(0, TextFit.PreviousBoundary("\U0001F600x", 2), "previous boundary");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("UpstreamRendering", "WidgetsUseCellWidths", "Tab headers, menus, bars, and checkboxes measure cell widths",
                        _ =>
                        {
                            TabView tabs = new TabView();
                            tabs.Add("\u4E2D\u6587", new Label(Text.From("a"))).Add("B", new Label(Text.From("b")));
                            Snapshot.RenderWidget(tabs, 20, 3);
                            tabs.HandleMouse(new MouseEvent(MouseEventKind.Press, MouseButton.Left, 7, 0, KeyModifiers.None, 1));
                            Check.Equal(1, tabs.ActiveIndex, "click past a wide header hits the second tab");
                            Check.Equal(8, new Checkbox("\u4E2D\u6587").Measure(new Size(40, 1)).Width, "checkbox width");
                            BarChart chart = new BarChart().Add("\u4E2D\u6587", 3).Add("ab", 1);
                            CellBuffer buffer = new CellBuffer(20, 2);
                            chart.Render(new BufferSurface(buffer));
                            Check.Equal("\u2588", buffer.Get(5, 0).Grapheme, "bars align after a wide label");
                            Check.Equal("\u2588", buffer.Get(5, 1).Grapheme, "and after a narrow one");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("UpstreamRendering", "CommandPalette", "Command palette ranks matches, runs the pick, and cancels",
                        _ =>
                        {
                            int ran = 0;
                            CommandRegistry registry = new CommandRegistry();
                            registry.Add(new Command("file.open", "Open File", () => ran++, "File", KeyChord.Parse("ctrl+o")));
                            registry.Add(new Command("view.palette", "Show Palette", () => { }, "View"));
                            registry.Add(new Command("file.close", "Close", () => { }, "File", null, null, () => false));
                            CommandPaletteModal palette = new CommandPaletteModal(registry);
                            Check.Equal(2, palette.Filtered.Count, "disabled hidden");
                            palette.HandleKey(KeyEvent.Char('o'));
                            palette.HandleKey(KeyEvent.Char('p'));
                            Check.Equal("file.open", palette.Highlighted!.Id, "prefix match ranks first");
                            CellBuffer screen = new CellBuffer(80, 20);
                            palette.Render(new BufferSurface(screen));
                            Check.True(Snapshot.ToText(screen).Contains("Ctrl+O") || Snapshot.ToText(screen).Contains("ctrl+o"), "chord shown");
                            palette.HandleKey(KeyEvent.Special(KeyCode.Enter));
                            Check.Equal(1, ran, "ran the command");
                            Check.True(palette.Completion.Result is Command, "completed with the command");
                            CommandPaletteModal cancelled = new CommandPaletteModal(registry);
                            cancelled.HandleKey(KeyEvent.Special(KeyCode.Escape));
                            Check.True(cancelled.Completion.Result == null, "Escape cancels");
                            Check.Equal(0, CommandPaletteModal.Score(registry.Commands[0], "zzz"), "no match scores zero");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("UpstreamRendering", "KeyHelp", "Key help groups by category, filters, and closes",
                        _ =>
                        {
                            CommandRegistry registry = new CommandRegistry();
                            registry.Add(new Command("a", "Save", () => { }, "File", KeyChord.Parse("ctrl+s")));
                            registry.Add(new Command("b", "Find", () => { }, "Edit", KeyChord.Parse("ctrl+f")));
                            registry.Add(new Command("c", "No chord", () => { }, "Edit"));
                            KeyHelpModal help = KeyHelpModal.FromCommands(registry.Commands).Add("List", "Up/Down", "Move");
                            Check.Equal(3, help.Entries.Count, "chorded commands plus extra rows");
                            CellBuffer screen = new CellBuffer(80, 24);
                            help.Render(new BufferSurface(screen));
                            string text = Snapshot.ToText(screen);
                            Check.True(text.Contains("File") && text.Contains("Edit") && text.Contains("Save") && text.Contains("Up/Down"), "grouped rows");
                            help.HandleKey(KeyEvent.Char('f'));
                            help.HandleKey(KeyEvent.Char('i'));
                            help.HandleKey(KeyEvent.Char('n'));
                            Check.Equal(1, help.MatchCount, "filtered to Find");
                            help.HandleKey(KeyEvent.Special(KeyCode.Escape));
                            Check.True(help.IsClosed, "closed");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("UpstreamRendering", "HostPalette", "ShowCommandPaletteAsync runs the chosen command through the host",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(60, 15);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                int ran = 0;
                                CommandRegistry registry = new CommandRegistry();
                                registry.Add(new Command("go", "Go Somewhere", () => ran++));
                                app.Start();
                                Task<Command?> pending = app.ShowCommandPaletteAsync(registry);
                                backend.FeedKey("enter");
                                app.PumpInputOnce();
                                Check.Equal(1, ran, "command ran");
                                Check.True(pending.Wait(5000), "palette completed");
                                Check.False(app.Modals.IsActive, "palette removed");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("UpstreamRendering", "MultiSeriesLine", "LineChart draws extra series in their colors and a legend",
                        _ =>
                        {
                            LineChart chart = new LineChart(new double[] { 0, 1, 2, 3 }) { ShowLegend = true, PrimaryName = "a" };
                            Color red = Color.FromPalette(1);
                            chart.AddSeries("b", new double[] { 3, 2, 1, 0 }, red);
                            CellBuffer buffer = new CellBuffer(20, 6);
                            chart.Render(new BufferSurface(buffer));
                            bool sawRed = false;
                            bool sawPrimary = false;
                            for (int y = 0; y < 5; y++)
                            {
                                for (int x = 0; x < 20; x++)
                                {
                                    Cell cell = buffer.Get(x, y);
                                    if (cell.Grapheme.Length == 1 && cell.Grapheme[0] > 0x2800)
                                    {
                                        sawRed |= cell.Style.Foreground == red;
                                        sawPrimary |= cell.Style.Foreground == chart.Color;
                                    }
                                }
                            }

                            Check.True(sawRed && sawPrimary, "both series visible in their colors");
                            Check.True(Snapshot.ToText(buffer).Split('\n')[5].Contains("b"), "legend row");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("UpstreamRendering", "StackedAndColumns", "BarChart stacks segments and ColumnChart draws vertical series",
                        _ =>
                        {
                            BarChart bars = new BarChart().AddStacked("m", 2, 2);
                            CellBuffer buffer = new CellBuffer(14, 1);
                            bars.Render(new BufferSurface(buffer));
                            Check.Equal("\u2588", buffer.Get(2, 0).Grapheme, "first segment glyph");
                            Check.Equal("\u2593", buffer.Get(7, 0).Grapheme, "second segment glyph");
                            Check.True(Snapshot.ToText(buffer).Contains("4"), "total printed");

                            ColumnChart columns = new ColumnChart { ShowScale = false };
                            columns.Labels.AddRange(new[] { "Mo", "Tu" });
                            columns.AddSeries("in", new double[] { 1, 2 }, Color.FromPalette(2));
                            columns.AddSeries("out", new double[] { 1, 0 }, Color.FromPalette(6), "#");
                            CellBuffer grid = new CellBuffer(16, 6);
                            columns.Render(new BufferSurface(grid));
                            string text = Snapshot.ToText(grid);
                            Check.True(text.Contains("Mo") && text.Contains("Tu"), "x labels");
                            Check.True(text.Contains("#"), "second series glyph stacked");
                            Check.True(text.Contains("out"), "legend");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("UpstreamRendering", "ProgrammaticModalClose", "A modal closed without input is dropped and never swallows the next key",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 10);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                List<KeyEvent> received = new List<KeyEvent>();
                                app.KeyReceived += k => received.Add(k);
                                app.Start();
                                ContextMenu menu = new ContextMenu(new[] { new MenuItem("x") }, 0, 0);
                                Task<object?> shown = app.ShowAsync(menu);
                                menu.RequestClose(-1);
                                Check.False(app.Modals.IsActive, "closed modal pruned");
                                backend.FeedInput("q");
                                app.PumpInputOnce();
                                Check.Equal(1, received.Count, "the next key reached the application");
                                Check.True(shown.IsCompleted, "completed");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("UpstreamRendering", "KeyEncoding", "Every function key and modified key round-trips through the parser",
                        _ =>
                        {
                            List<KeyEvent> keys = new List<KeyEvent>();
                            for (KeyCode code = KeyCode.F1; code <= KeyCode.F12; code++)
                            {
                                keys.Add(KeyEvent.Special(code));
                                keys.Add(KeyEvent.Special(code, KeyModifiers.Shift));
                            }

                            keys.Add(KeyEvent.Special(KeyCode.Up, KeyModifiers.Alt));
                            keys.Add(KeyEvent.Special(KeyCode.PageDown, KeyModifiers.Ctrl));
                            keys.Add(KeyEvent.Special(KeyCode.Delete));
                            keys.Add(KeyEvent.Special(KeyCode.Tab, KeyModifiers.Shift));
                            keys.Add(KeyEvent.Char('p', KeyModifiers.Ctrl));
                            keys.Add(KeyEvent.Char('x', KeyModifiers.Alt));

                            for (int i = 0; i < keys.Count; i++)
                            {
                                InputParser parser = new InputParser();
                                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(KeySequenceEncoder.Encode(keys[i]));
                                parser.Feed(bytes, bytes.Length);
                                List<InputEvent> events = new List<InputEvent>(parser.Drain());
                                events.AddRange(parser.Flush());
                                Check.Equal(1, events.Count, "one event for " + keys[i]);
                                Check.Equal(keys[i].Code, events[0].Key.Code, "code for " + keys[i]);
                                Check.Equal(keys[i].Modifiers, events[0].Key.Modifiers, "modifiers for " + keys[i]);
                            }

                            HeadlessBackend backend = new HeadlessBackend(20, 5);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                List<KeyEvent> received = new List<KeyEvent>();
                                app.KeyReceived += k => received.Add(k);
                                app.Start();
                                backend.FeedKey("f9");
                                backend.FeedKey(KeyEvent.Special(KeyCode.F8));
                                app.PumpInputOnce();
                                app.PumpInputOnce();
                                Check.Equal(2, received.Count, "both keys");
                                Check.Equal(KeyCode.F9, received[0].Code, "F9");
                                Check.Equal(KeyCode.F8, received[1].Code, "F8");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        })
                });
        }
    }
}

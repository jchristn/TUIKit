namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Hosting;
    using TUIKit.Input;
    using TUIKit.Layout;
    using TUIKit.Rendering;
    using TUIKit.Terminal;

    /// <summary>End-to-end and negative coverage for portable terminal hyperlinks.</summary>
    public static class HyperlinkSuite
    {
        /// <summary>Builds the suite shared by every test runner.</summary>
        /// <returns>The descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor("Hyperlinks", "Portable OSC 8 Hyperlinks", new List<TestCaseDescriptor>
            {
                Case("UrlAndFiles", () =>
                {
                    foreach (Uri uri in new[]
                    {
                        new Uri("https://example.com/a?q=1#section"),
                        new Uri(Path.Combine(Path.GetTempPath(), "my settings #1.json")),
                        new Uri("file:///C:/My%20Project/settings.json"),
                        new Uri("file:///home/user/my%20settings.json"),
                        new Uri("mailto:hello@example.com")
                    })
                    {
                        StyledText text = StyledText.Link("open", uri).Bold().Cyan().Style(CellStyle.Default);
                        Check.Equal(uri.AbsoluteUri, text.Spans[0].Style.Hyperlink!.AbsoluteUri, "Style transforms preserve target");
                        Check.Equal(4, text.Width, "URI contributes no display width");
                        string output = AnsiText.Render(text, TerminalColorDepth.None, true);
                        Check.True(output.Contains(";" + uri.AbsoluteUri + "\u001b\\"), "Encoded absolute target emitted");
                        Check.Equal("open", AnsiStripper.Strip(output), "Visible label only");
                        Check.True(output.EndsWith(Ansi.CloseHyperlink, StringComparison.Ordinal), "No link leaks");
                    }
                }),
                Case("InvalidTargets", () =>
                {
                    Check.Throws<ArgumentNullException>(() => StyledText.Link("x", null!), "Null target");
                    Check.Throws<ArgumentNullException>(() => StyledText.Link(null!, new Uri("https://example.com")), "Null label");
                    foreach (string value in new[] { "relative/path", "javascript:alert(1)", "data:text/plain,hello", "https://example.com/\u001b]52;c;bad\a", "https://example.com/\nnext", "https://example.com/\u009c" })
                        Check.Throws<ArgumentException>(() => StyledText.Link("x", new Uri(value, UriKind.RelativeOrAbsolute)), "Unsafe target rejected");
                    Check.Throws<ArgumentNullException>(() => Ansi.OpenHyperlink(null, null!), "Null raw target");
                    Check.Throws<ArgumentException>(() => Ansi.OpenHyperlink(null, "relative"), "Relative raw target");
                    foreach (string id in new[] { "bad:id", "bad;id", "bad=id", "bad\a", "bad\u001b\\" })
                        Check.Throws<ArgumentException>(() => Ansi.OpenHyperlink(id, "https://example.com"), "Unsafe ID rejected");
                }),
                Case("EqualityAndRemoval", () =>
                {
                    CellStyle first = StyledText.Link("x", new Uri("https://example.com/#a")).Spans[0].Style;
                    CellStyle second = first.WithHyperlink(new Uri("https://example.com/#b"));
                    Check.False(first.Equals(second), "Fragments participate in equality");
                    Check.Equal(first.GetHashCode(), first.WithHyperlink(new Uri("https://example.com/#a")).GetHashCode(), "Equal targets hash equally");
                    Check.True(first.WithForeground(Color.FromPalette(1)).Over(CellStyle.Default).Hyperlink != null, "Composition preserves link");
                    Check.True(first.WithHyperlink(null).Hyperlink == null, "Explicit removal");
                }),
                Case("FlowBoundaries", () =>
                {
                    StyledText text = StyledText.Link("one\r\ntwo", new Uri("https://one.example"))
                        .Append(" plain ").Append(StyledText.Link("three", new Uri("https://two.example")));
                    string output = AnsiText.Render(text, TerminalColorDepth.TrueColor, true);
                    Check.True(output.Contains(Ansi.CloseHyperlink + "\r\n"), "Closed before newline");
                    Check.Equal(3, Count(output, "\u001b]8;id="), "Reopened after newline and for next target");
                    Check.Equal(3, Count(output, Ansi.CloseHyperlink), "Balanced links");
                    Check.Equal("one\r\ntwo plain three", AnsiStripper.Strip(output), "Text preserved");
                    Check.False(AnsiText.Render(text, TerminalColorDepth.TrueColor).Contains("\u001b]8;"), "Legacy overload unchanged");
                    Check.Equal(text.ToPlainString(), AnsiText.Render(text, TerminalColorDepth.None), "Legacy plain output");
                    Check.Equal(string.Empty, AnsiText.Render(StyledText.Link("", new Uri("https://example.com")), TerminalColorDepth.TrueColor, true), "Empty link emits nothing");
                }),
                Case("InlineClippingAndWideGlyphs", () =>
                {
                    CellBuffer buffer = new CellBuffer(5, 2);
                    BufferSurface surface = new BufferSurface(buffer);
                    StyledText text = StyledText.Link("界abc", new Uri("https://example.com"));
                    surface.DrawStyledText(0, 0, text);
                    surface.DrawStyledText(3, 1, text);
                    IReadOnlyList<string> lines = InlineRenderer.ToAnsiLines(buffer, TerminalColorDepth.None, true);
                    Check.Equal("界abc", AnsiStripper.Strip(lines[0]), "Wide continuation skipped");
                    Check.Equal("   界", AnsiStripper.Strip(lines[1]), "Right clipping preserved");
                    foreach (string line in lines)
                    {
                        Check.Equal(1, Count(line, "\u001b]8;id="), "One opener per row");
                        Check.True(line.EndsWith(Ansi.CloseHyperlink, StringComparison.Ordinal), "Closed at row end");
                    }
                    Check.False(InlineRenderer.ToAnsiLines(buffer, TerminalColorDepth.None)[0].Contains("\u001b"), "Plain snapshot no escapes");
                }),
                Case("RendererDiffAndRemoval", () =>
                {
                    using (HeadlessBackend backend = new HeadlessBackend(8, 2))
                    {
                        TerminalRenderer renderer = new TerminalRenderer(8, 2, TerminalColorDepth.TrueColor);
                        StyledText text = StyledText.Link("same", new Uri("https://example.com/#first"));
                        Action<ISurface> draw = surface => surface.DrawStyledText(0, 0, text);
                        renderer.Render(backend, draw);
                        string first = backend.TakeOutput();
                        Check.True(first.Contains("#first"), "Auto honors capability");
                        Check.True(first.IndexOf(Ansi.CloseHyperlink, StringComparison.Ordinal) < first.IndexOf(Ansi.MoveTo(0, 1), StringComparison.Ordinal), "Closed before moving cursor");
                        Check.False(renderer.Render(backend, draw), "Unchanged frame skipped");
                        text = StyledText.Link("same", new Uri("https://example.com/#second"));
                        Check.True(renderer.Render(backend, draw), "Target-only change repaints");
                        Check.True(backend.TakeOutput().Contains("#second"), "New target emitted");
                        text = StyledText.From("same");
                        renderer.Render(backend, draw);
                        Check.False(backend.TakeOutput().Contains("\u001b]8;id="), "Removed link not re-emitted");
                        text = StyledText.Link("same", new Uri("https://example.com"));
                        renderer.Hyperlinks = HyperlinkMode.Never;
                        renderer.Render(backend, draw);
                        Check.False(backend.TakeOutput().Contains("\u001b]8;"), "Never disables links");
                        renderer.Hyperlinks = HyperlinkMode.Always;
                        Check.True(renderer.Render(backend, draw), "Mode-only change repaints");
                        Check.True(backend.TakeOutput().Contains("\u001b]8;id="), "Always enables links");
                        backend.Resize(10, 3);
                        renderer.Render(backend, draw);
                        Check.True(backend.TakeOutput().Contains("\u001b]8;id="), "Resize restores target");
                    }
                }),
                Case("UnsupportedBackend", () =>
                {
                    TerminalCapabilities caps = CapabilityDetector.Detect(_ => null, false);
                    using (HeadlessBackend backend = new HeadlessBackend(8, 1, caps))
                    {
                        TerminalRenderer renderer = new TerminalRenderer(8, 1, TerminalColorDepth.None);
                        Action<ISurface> draw = surface => surface.DrawStyledText(0, 0, StyledText.Link("link", new Uri("https://example.com")));
                        renderer.Render(backend, draw);
                        Check.False(backend.TakeOutput().Contains("\u001b]8;"), "Auto fallback");
                        renderer.Hyperlinks = HyperlinkMode.Always;
                        renderer.Render(backend, draw);
                        Check.True(backend.TakeOutput().Contains("\u001b]8;id="), "Explicit override with no color");
                    }
                }),
                Case("LegacyRegistryTargets", () =>
                {
                    using (HeadlessBackend backend = new HeadlessBackend(8, 1))
                    {
                        string? target = "https://first.example";
                        TerminalRenderer renderer = new TerminalRenderer(8, 1, TerminalColorDepth.TrueColor)
                        {
                            LinkResolver = _ => target
                        };
                        Action<ISurface> draw = surface => surface.DrawText(0, 0, "link", CellStyle.Default.WithLinkId(1));
                        renderer.Render(backend, draw);
                        Check.True(backend.TakeOutput().Contains("first.example"), "Legacy target resolved");
                        target = "https://second.example";
                        Check.True(renderer.Render(backend, draw), "Reused frame ID with new URI repaints");
                        Check.True(backend.TakeOutput().Contains("second.example"), "New registry URI");
                        target = "javascript:alert(1)";
                        renderer.Render(backend, draw);
                        Check.False(backend.TakeOutput().Contains("\u001b]8;"), "Unsafe registry URI ignored");
                        target = null;
                        renderer.Invalidate();
                        renderer.Render(backend, draw);
                        Check.False(backend.TakeOutput().Contains("\u001b]8;"), "Command-only link stays application-only");
                    }
                }),
                Case("MarkdownWrapping", () =>
                {
                    IReadOnlyList<StyledText> rows = MarkdownRenderer.Render("[**many words across rows**](https://example.com/docs)", 12);
                    int linkedRows = 0;
                    foreach (StyledText row in rows)
                    {
                        string output = AnsiText.Render(row, TerminalColorDepth.None, true);
                        if (output.Contains("\u001b]8;id="))
                        {
                            linkedRows++;
                            Check.Equal(Count(output, "\u001b]8;id="), Count(output, Ansi.CloseHyperlink), "Wrapped row balanced");
                        }
                    }
                    Check.True(linkedRows > 1, "Wrapped labels retain targets");
                    foreach (string markdown in new[] { "[bad](javascript:alert)", "[relative](../README.md)", "`[code](https://example.com)`" })
                    {
                        foreach (StyledText row in MarkdownRenderer.Render(markdown, 80))
                            Check.False(AnsiText.Render(row, TerminalColorDepth.None, true).Contains("\u001b]8;"), "Unsafe, unresolved relative, and code links stay literal");
                    }
                    foreach (StyledText row in MarkdownRenderer.Render("<https://example.com> <hello@example.com>", 80))
                    {
                        string output = AnsiText.Render(row, TerminalColorDepth.None, true);
                        Check.True(output.Contains("https://example.com"), "URL autolink");
                        Check.True(output.Contains("mailto:hello@example.com"), "Email autolink");
                    }
                }),
                Case("WriterPolicy", () =>
                {
                    using (StringWriter writer = new StringWriter())
                    {
                        StyledConsole console = new StyledConsole(writer, TerminalColorDepth.None);
                        StyledText text = StyledText.Link("docs", new Uri("https://example.com"));
                        console.WriteLine(text);
                        Check.Equal("docs\n", writer.ToString(), "Redirected/custom writer plain in Auto");
                        writer.GetStringBuilder().Clear();
                        console.Hyperlinks = HyperlinkMode.Always;
                        console.WriteLine(text);
                        Check.True(writer.ToString().Contains("\u001b]8;id="), "Always emits links without color");
                        writer.GetStringBuilder().Clear();
                        console.Hyperlinks = HyperlinkMode.Never;
                        console.WriteLine(text);
                        Check.Equal("docs\n", writer.ToString(), "Never plain");
                    }
                }),
                Case("HostIntegration", () =>
                {
                    using (HeadlessBackend backend = new HeadlessBackend(30, 5))
                    using (TuiApplication app = new TuiApplication(backend))
                    {
                        app.Layout = Layout.Create().Add("main", region => region.FillWidth().FillHeight()).Build();
                        Pane pane = new Pane("main");
                        LinkRegistry registry = new LinkRegistry();
                        Link link = registry.Add("legacy", new Rect(0, 0, 6, 1), "https://legacy.example");
                        pane.WriteLine(StyledText.From("legacy", CellStyle.Default.WithLinkId(link.Id)));
                        pane.WriteLine(StyledText.Link("direct", new Uri("https://direct.example")));
                        app.BindPane("main", pane);
                        app.Start();
                        app.Links = registry;
                        backend.TakeOutput();
                        app.RenderOnce();
                        string output = backend.TakeOutput();
                        Check.True(output.Contains("https://legacy.example"), "Host resolves registry targets");
                        Check.True(output.Contains("https://direct.example"), "Pane retains immutable target");
                        app.Hyperlinks = HyperlinkMode.Never;
                        app.RenderOnce();
                        Check.False(backend.TakeOutput().Contains("\u001b]8;"), "Host policy changes apply after starting");
                        app.Stop();
                    }
                }),
                Case("Detection", () =>
                {
                    Dictionary<string, string> env = new Dictionary<string, string>();
                    Func<string, string?> get = key => env.TryGetValue(key, out string? value) ? value : null;
                    env["COLORTERM"] = "truecolor";
                    Check.False(CapabilityDetector.Detect(get, true).Hyperlinks, "Color is not hyperlink support");
                    env["TERM_PROGRAM"] = "Apple_Terminal";
                    Check.False(CapabilityDetector.Detect(get, true).Hyperlinks, "Unknown Apple Terminal support is conservative");
                    foreach (string program in new[] { "iTerm.app", "WezTerm", "ghostty", "kitty", "Alacritty" })
                    {
                        env["TERM_PROGRAM"] = program;
                        env["NO_COLOR"] = "1";
                        Check.True(CapabilityDetector.Detect(get, true).Hyperlinks, "Known terminal independent of NO_COLOR");
                        Check.Equal(TerminalColorDepth.None, CapabilityDetector.Detect(get, true).ColorDepth, "NO_COLOR retained");
                    }
                    env.Clear(); env["WT_SESSION"] = "session";
                    Check.True(CapabilityDetector.Detect(get, true).Hyperlinks, "Windows Terminal");
                    env.Clear(); env["VTE_VERSION"] = "5000";
                    Check.True(CapabilityDetector.Detect(get, true).Hyperlinks, "VTE version");
                    env["VTE_VERSION"] = "4999";
                    Check.False(CapabilityDetector.Detect(get, true).Hyperlinks, "Older VTE");
                    env.Clear(); env["TERM"] = "foot";
                    Check.True(CapabilityDetector.Detect(get, true).Hyperlinks, "Linux foot");
                    env["TMUX"] = "socket";
                    Check.False(CapabilityDetector.Detect(get, true).Hyperlinks, "Multiplexer requires opt-in");
                    env.Clear(); env["TERM_PROGRAM"] = "iTerm.app"; env["TERM"] = "screen-256color";
                    Check.False(CapabilityDetector.Detect(get, true).Hyperlinks, "GNU screen conservative");
                    env["TERM"] = "dumb";
                    Check.False(CapabilityDetector.Detect(get, true).Hyperlinks, "Dumb terminal");
                    Check.False(CapabilityDetector.Detect(get, false).Hyperlinks, "Redirected output");
                })
            });
        }

        private static TestCaseDescriptor Case(string id, Action action)
        {
            return new TestCaseDescriptor("Hyperlinks", id, id, _ => { action(); return Task.CompletedTask; });
        }

        private static int Count(string text, string value)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }
            return count;
        }
    }
}

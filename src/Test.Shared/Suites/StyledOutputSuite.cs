namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Rendering;
    using TUIKit.Terminal;
    using TUIKit.Testing;
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for the styled one-shot output surface (TUIKIT_GAPS G1–G6): markup escaping,
    /// styled-text/cell-buffer to ANSI, capability resolution, the <see cref="StyledConsole"/> writer,
    /// and the extended <see cref="Table"/>.
    /// </summary>
    public static class StyledOutputSuite
    {
        /// <summary>
        /// Builds the styled-output suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "StyledOutput",
                displayName: "Styled Output (Markup.Escape / AnsiText / StyledConsole / Table)",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("StyledOutput", "MarkupEscape", "Markup.Escape round-trips literal text (G1)",
                        _ =>
                        {
                            Check.Equal("[dim]x[/]", Markup.Parse(Markup.Escape("[dim]x[/]")).ToPlainString(), "escaped markup renders literally");
                            Check.Equal("a[b]c", Markup.Parse(Markup.Escape("a[b]c")).ToPlainString(), "round trip preserves brackets");
                            Check.Equal(string.Empty, Markup.Escape(string.Empty), "empty escapes to empty");
                            Check.Throws<ArgumentNullException>(() => Markup.Escape(null!), "null rejected");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("StyledOutput", "AnsiText", "AnsiText.Render is additive color with no cursor moves (G2)",
                        _ =>
                        {
                            StyledText st = Markup.Parse("[bold red]hi[/]");
                            string truecolor = AnsiText.Render(st, TerminalColorDepth.TrueColor);
                            Check.Equal(st.ToPlainString(), AnsiStripper.Strip(truecolor), "stripping color yields the plain text");
                            Check.True(truecolor.Contains("\u001b["), "truecolor emits an SGR sequence");

                            string sgrRemoved = Regex.Replace(truecolor, "\u001b\\[[0-9;]*m", string.Empty);
                            Check.False(sgrRemoved.Contains("\u001b"), "only SGR escapes are emitted (no cursor moves)");

                            string plain = AnsiText.Render(st, TerminalColorDepth.None);
                            Check.Equal(st.ToPlainString(), plain, "None depth is plain");
                            Check.False(plain.Contains("\u001b"), "None depth has no escapes");

                            Check.True(AnsiText.Render("[green]ok[/]", TerminalColorDepth.Ansi16).Contains("\u001b["), "markup overload renders SGR");
                            Check.Throws<ArgumentNullException>(() => AnsiText.Render((StyledText)null!, TerminalColorDepth.TrueColor), "null styled text");
                            Check.Throws<ArgumentNullException>(() => AnsiText.Render((string)null!, TerminalColorDepth.TrueColor), "null markup");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("StyledOutput", "InlineRenderer", "InlineRenderer matches Snapshot text and adds color (G3)",
                        _ =>
                        {
                            CellBuffer buffer = new CellBuffer(12, 2);
                            BufferSurface surface = new BufferSurface(buffer);
                            surface.DrawStyledText(0, 0, Markup.Parse("[green]ok[/]"));

                            IReadOnlyList<string> plainLines = InlineRenderer.ToAnsiLines(buffer, TerminalColorDepth.None);
                            Check.Equal(Snapshot.ToText(buffer), string.Join("\n", plainLines), "None lines equal Snapshot.ToText");

                            IReadOnlyList<string> colored = InlineRenderer.ToAnsiLines(buffer, TerminalColorDepth.TrueColor);
                            Check.Equal(Snapshot.ToText(buffer), AnsiStripper.Strip(string.Join("\n", colored)), "stripped colored equals plain");
                            Check.True(colored[0].Contains("\u001b["), "colored first row carries an SGR run");

                            Check.Throws<ArgumentNullException>(() => InlineRenderer.ToAnsiLines(null!, TerminalColorDepth.None), "null buffer");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("StyledOutput", "Capabilities", "NO_COLOR and output resolution behave correctly (G4)",
                        _ =>
                        {
                            Dictionary<string, string?> env = new Dictionary<string, string?>
                            {
                                { "COLORTERM", "truecolor" },
                                { "NO_COLOR", "1" }
                            };
                            Check.Equal(TerminalColorDepth.None, CapabilityDetector.Detect(name => env.TryGetValue(name, out string? v) ? v : null, true).ColorDepth, "NO_COLOR forces None even with truecolor");

                            Dictionary<string, string?> color = new Dictionary<string, string?> { { "COLORTERM", "truecolor" } };
                            Check.Equal(TerminalColorDepth.TrueColor, CapabilityDetector.Detect(name => color.TryGetValue(name, out string? v) ? v : null, true).ColorDepth, "truecolor detected when NO_COLOR unset");

                            Dictionary<string, string?> dumb = new Dictionary<string, string?> { { "TERM", "dumb" } };
                            Check.Equal(TerminalColorDepth.None, CapabilityDetector.Detect(name => dumb.TryGetValue(name, out string? v) ? v : null, true).ColorDepth, "TERM=dumb is None");

                            Check.Equal(TerminalColorDepth.None, CapabilityDetector.ResolveOutputColorDepth(new StringWriter()), "a plain StringWriter resolves to None");
                            Check.Throws<ArgumentNullException>(() => CapabilityDetector.ResolveOutputColorDepth(null!), "null writer");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("StyledOutput", "StyledConsolePlain", "StyledConsole writes plain when depth is None (G5)",
                        _ =>
                        {
                            StringWriter writer = new StringWriter();
                            StyledConsole console = new StyledConsole(writer, TerminalColorDepth.None);
                            Check.Equal(TerminalColorDepth.None, console.ColorDepth, "depth exposed");
                            Check.Equal(80, console.DefaultWidth, "default width 80");

                            console.MarkupLine("[bold red]hi[/]");
                            Check.Equal("hi\n", writer.ToString(), "None writes plain text with a newline");

                            Check.Throws<ArgumentOutOfRangeException>(() => console.DefaultWidth = 0, "default width min 1");
                            Check.Throws<ArgumentNullException>(() => new StyledConsole(null!, TerminalColorDepth.None), "null output");
                            Check.Throws<ArgumentNullException>(() => console.Markup(null!), "null markup");
                            Check.Throws<ArgumentNullException>(() => console.Write((StyledText)null!), "null styled text");
                            Check.Throws<ArgumentNullException>(() => console.Write((IWidget)null!), "null widget");
                            Check.Throws<ArgumentOutOfRangeException>(() => console.Write(new Label(Text.From("x")), 0), "per-call width min 1");
                            Check.Throws<ArgumentOutOfRangeException>(() => console.WriteLine(new Label(Text.From("x")), -1), "per-call width negative");

                            StringWriter widthWriter = new StringWriter();
                            StyledConsole widthConsole = new StyledConsole(widthWriter, TerminalColorDepth.None);
                            widthConsole.Write(new Label(Text.From("hi")), 5); // explicit valid width renders without throwing
                            Check.Equal("hi", widthWriter.ToString().TrimEnd(), "explicit width renders the widget");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("StyledOutput", "StyledConsoleColor", "StyledConsole emits color and flows inline (G5)",
                        _ =>
                        {
                            StringWriter writer = new StringWriter();
                            StyledConsole console = new StyledConsole(writer, TerminalColorDepth.TrueColor);
                            console.MarkupLine("[bold red]hi[/]");
                            string output = writer.ToString();
                            Check.True(output.Contains("\u001b["), "color emits SGR");
                            Check.Equal("hi", AnsiStripper.Strip(output).TrimEnd(), "stripped equals the text");

                            StringWriter flow = new StringWriter();
                            StyledConsole plain = new StyledConsole(flow, TerminalColorDepth.None);
                            plain.MarkupLine("one");
                            plain.MarkupLine("two");
                            Check.Equal("one\ntwo\n", flow.ToString(), "multiple lines flow inline, not overwrite");

                            string sgrRemoved = Regex.Replace(output, "\u001b\\[[0-9;]*m", string.Empty);
                            Check.False(sgrRemoved.Contains("\u001b"), "no cursor-move sequences");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("StyledOutput", "TableBackCompat", "Default Table keeps its no-border, even-column behavior (G6)",
                        _ =>
                        {
                            Table table = new Table(new[] { "Metric", "Value" });
                            table.AddRow(new[] { "cpu", "62%" });
                            string text = Snapshot.RenderWidget(table, 24, 3);
                            Check.True(text.Contains("Metric") && text.Contains("Value"), "headers rendered");
                            Check.True(text.Contains("cpu") && text.Contains("62%"), "row rendered");
                            Check.False(text.Contains("│") || text.Contains("╭") || text.Contains("┌"), "no border by default");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("StyledOutput", "TableBordersAndStyle", "Rounded Table draws borders and keeps styled cells (G6)",
                        _ =>
                        {
                            Table table = new Table(new[] { "Name", "State" }, TableBorder.Rounded);
                            table.Sizing = ColumnSizing.FitContent;
                            table.AddMarkupRow("build", "[green]ok[/]");
                            table.AddMarkupRow("test", "[red]fail[/]");

                            string text = Snapshot.RenderWidget(table, 30, 6);
                            Check.True(text.Contains("╭") && text.Contains("╯"), "rounded corners drawn");
                            Check.True(text.Contains("│") && text.Contains("─"), "borders drawn");
                            Check.True(text.Contains("ok") && text.Contains("fail"), "styled cell text present");

                            CellBuffer buffer = new CellBuffer(30, 6);
                            table.Render(new BufferSurface(buffer));
                            string colored = string.Join("\n", InlineRenderer.ToAnsiLines(buffer, TerminalColorDepth.TrueColor));
                            Check.True(colored.Contains("\u001b["), "styled cell survives to an SGR run");

                            Check.Throws<ArgumentOutOfRangeException>(() => table.SetAlignment(9, CellAlignment.Right), "alignment column out of range");
                            Check.Throws<ArgumentNullException>(() => table.AddMarkupRow(null!), "null markup row");
                            Check.Throws<ArgumentNullException>(() => table.AddRow((StyledText[])null!), "null styled row");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}

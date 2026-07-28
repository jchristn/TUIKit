namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Rendering;
    using TUIKit.Terminal;

    /// <summary>
    /// Touchstone suite covering the double-buffered <see cref="TerminalRenderer"/>: initial paint,
    /// no-op frames, row-level diffing, resize invalidation, and color emission.
    /// </summary>
    public static class RenderSuite
    {
        /// <summary>
        /// Builds the render suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Render",
                displayName: "Terminal Renderer",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Render", "InitialPaint", "First frame paints content",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(20, 5);
                            TerminalRenderer renderer = new TerminalRenderer(20, 5, TerminalColorDepth.TrueColor);
                            bool emitted = renderer.Render(backend, s => s.DrawText(0, 0, "Hi", CellStyle.Default));
                            Check.True(emitted, "First frame emits output");
                            Check.True(backend.PeekOutput().Contains("Hi"), "Output contains drawn text");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Render", "NoChangeNoOutput", "Unchanged frame emits nothing",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(20, 5);
                            TerminalRenderer renderer = new TerminalRenderer(20, 5, TerminalColorDepth.TrueColor);
                            renderer.Render(backend, s => s.DrawText(0, 0, "Hi", CellStyle.Default));
                            backend.TakeOutput();

                            bool emitted = renderer.Render(backend, s => s.DrawText(0, 0, "Hi", CellStyle.Default));
                            Check.False(emitted, "Identical frame emits nothing");
                            Check.Equal("", backend.PeekOutput(), "No bytes written");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Render", "RowLevelDiff", "Only changed rows are repainted",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(20, 6);
                            TerminalRenderer renderer = new TerminalRenderer(20, 6, TerminalColorDepth.TrueColor);
                            renderer.Render(backend, s =>
                            {
                                s.DrawText(0, 0, "row0", CellStyle.Default);
                                s.DrawText(0, 3, "row3", CellStyle.Default);
                            });
                            backend.TakeOutput();

                            renderer.Render(backend, s =>
                            {
                                s.DrawText(0, 0, "ROW0", CellStyle.Default);
                                s.DrawText(0, 3, "row3", CellStyle.Default);
                            });
                            string output = backend.PeekOutput();
                            Check.True(output.Contains(Ansi.MoveTo(0, 0)), "Row 0 repainted");
                            Check.True(output.Contains("ROW0"), "New content present");
                            Check.False(output.Contains(Ansi.MoveTo(0, 3)), "Unchanged row 3 not repainted");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Render", "ResizeInvalidates", "Resize forces a full repaint",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(20, 4);
                            TerminalRenderer renderer = new TerminalRenderer(20, 4, TerminalColorDepth.TrueColor);
                            renderer.Render(backend, s => s.DrawText(0, 0, "x", CellStyle.Default));
                            backend.TakeOutput();

                            backend.Resize(24, 5);
                            renderer.Render(backend, s => s.DrawText(0, 0, "x", CellStyle.Default));
                            Check.Equal(new Size(24, 5), renderer.Size, "Renderer adopted new size");
                            Check.True(backend.PeekOutput().Contains(Ansi.MoveTo(0, 4)), "Bottom row repainted after resize");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Render", "ColorEmission", "Styled cells emit SGR color",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(20, 3);
                            TerminalRenderer renderer = new TerminalRenderer(20, 3, TerminalColorDepth.TrueColor);
                            CellStyle red = CellStyle.Default.WithForeground(Color.FromRgb(200, 0, 0));
                            renderer.Render(backend, s => s.DrawText(0, 0, "R", red));
                            Check.True(backend.PeekOutput().Contains("38;2;200;0;0"), "Truecolor SGR emitted");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}

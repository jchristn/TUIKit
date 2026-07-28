namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Terminal;

    /// <summary>
    /// Touchstone suite covering the terminal backend layer: the headless backend, capability
    /// detection, color quantization, and ANSI sequence building.
    /// </summary>
    public static class TerminalSuite
    {
        /// <summary>
        /// Builds the terminal suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Terminal",
                displayName: "Terminal Backend and ANSI",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Terminal", "HeadlessCapture", "Headless backend captures and clears output",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(100, 30);
                            backend.Start();
                            backend.Write("hello");
                            backend.Write(" world");
                            Check.Equal("hello world", backend.PeekOutput(), "Peek");
                            Check.Equal("hello world", backend.TakeOutput(), "Take");
                            Check.Equal("", backend.PeekOutput(), "Cleared after take");
                            Check.True(backend.IsStarted, "Started flag");
                            Check.Equal(new Size(100, 30), backend.Size, "Size");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Terminal", "HeadlessInput", "Headless backend queues and drains input",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend();
                            backend.FeedInput("abc");
                            byte[] buffer = new byte[10];
                            int read = backend.ReadInput(buffer, 0, buffer.Length);
                            Check.Equal(3, read, "Bytes read");
                            Check.Equal((byte)'a', buffer[0], "First byte");
                            Check.Equal(0, backend.ReadInput(buffer, 0, buffer.Length), "Drained");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Terminal", "HeadlessResize", "Headless backend simulates resize",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(80, 24);
                            backend.Resize(120, 40);
                            Check.Equal(new Size(120, 40), backend.Size, "Resized");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Terminal", "DetectNonInteractive", "Non-interactive yields no capabilities",
                        _ =>
                        {
                            TerminalCapabilities caps = CapabilityDetector.Detect(_ => null, false);
                            Check.Equal(TerminalColorDepth.None, caps.ColorDepth, "Color depth");
                            Check.False(caps.EnhancedKeyboard, "Enhanced keyboard");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Terminal", "DetectWindowsTerminal", "WT_SESSION implies truecolor + enhanced",
                        _ =>
                        {
                            TerminalCapabilities caps = CapabilityDetector.Detect(
                                name => name == "WT_SESSION" ? "1" : null, true);
                            Check.Equal(TerminalColorDepth.TrueColor, caps.ColorDepth, "Color depth");
                            Check.True(caps.EnhancedKeyboard, "Enhanced keyboard");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Terminal", "DetectDumbAnd256", "TERM drives color depth",
                        _ =>
                        {
                            TerminalCapabilities dumb = CapabilityDetector.Detect(
                                name => name == "TERM" ? "dumb" : null, true);
                            Check.Equal(TerminalColorDepth.None, dumb.ColorDepth, "dumb terminal");

                            TerminalCapabilities palette = CapabilityDetector.Detect(
                                name => name == "TERM" ? "xterm-256color" : null, true);
                            Check.Equal(TerminalColorDepth.Palette256, palette.ColorDepth, "256 color");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Terminal", "SgrTruecolor", "SGR encodes truecolor and attributes",
                        _ =>
                        {
                            CellStyle style = CellStyle.Default
                                .WithForeground(Color.FromRgb(255, 0, 0))
                                .WithAttribute(CellAttributes.Bold, true);
                            string sgr = Ansi.Sgr(style, TerminalColorDepth.TrueColor);
                            Check.True(sgr.Contains(";1"), "Bold code present");
                            Check.True(sgr.Contains("38;2;255;0;0"), "Truecolor foreground present");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Terminal", "SgrDegrade", "SGR degrades truecolor to ANSI 16",
                        _ =>
                        {
                            CellStyle style = CellStyle.Default.WithForeground(Color.FromRgb(255, 0, 0));
                            string sgr = Ansi.Sgr(style, TerminalColorDepth.Ansi16);
                            Check.False(sgr.Contains("38;2;"), "No truecolor triplet when degraded");
                            Check.True(sgr.Contains("91"), "Bright red ANSI code present");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Terminal", "MoveToOneBased", "Cursor move converts to one-based",
                        _ =>
                        {
                            Check.Equal(Ansi.Csi + "1;1H", Ansi.MoveTo(0, 0), "Origin");
                            Check.Equal(Ansi.Csi + "3;6H", Ansi.MoveTo(5, 2), "Offset");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Terminal", "Quantize", "Color quantization picks nearest palette entries",
                        _ =>
                        {
                            Check.Equal((byte)9, ColorQuantizer.ToAnsi16(255, 0, 0), "Bright red to ANSI 16");
                            Check.Equal((byte)0, ColorQuantizer.ToAnsi16(0, 0, 0), "Black to ANSI 16");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}

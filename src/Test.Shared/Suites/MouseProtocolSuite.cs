namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit.Input;
    using TUIKit.Terminal;

    /// <summary>
    /// Touchstone suite covering positive SGR mouse protocol decoding: presses, releases, hover and
    /// drag motion, vertical and horizontal wheel, modifiers, wide coordinates, focus reporting, and
    /// the enable/disable escape sequences and capability detection behind them.
    /// </summary>
    public static class MouseProtocolSuite
    {
        private const string Esc = "";

        /// <summary>
        /// Builds the mouse protocol suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "MouseProtocol",
                displayName: "Mouse Protocol Decoding",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("MouseProtocol", "PressRelease", "SGR press and release decode with 0-based coordinates",
                        _ =>
                        {
                            IReadOnlyList<InputEvent> events = Parse(Esc + "[<0;5;3M" + Esc + "[<0;5;3m");
                            Check.Equal(2, events.Count, "Two events");

                            MouseEvent press = events[0].Mouse!;
                            Check.Equal((int)MouseEventKind.Press, (int)press.Kind, "First is press");
                            Check.Equal((int)MouseButton.Left, (int)press.Button, "Left button");
                            Check.Equal(4, press.X, "Wire column 5 becomes 4");
                            Check.Equal(2, press.Y, "Wire row 3 becomes 2");
                            Check.Equal(1, press.ClickCount, "Press carries click count 1");

                            MouseEvent release = events[1].Mouse!;
                            Check.Equal((int)MouseEventKind.Release, (int)release.Kind, "Second is release");
                            Check.Equal(0, release.ClickCount, "Release carries click count 0");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseProtocol", "AllButtons", "Middle and right presses map to their buttons",
                        _ =>
                        {
                            IReadOnlyList<InputEvent> events = Parse(Esc + "[<1;1;1M" + Esc + "[<2;1;1M");
                            Check.Equal(2, events.Count, "Two events");
                            Check.Equal((int)MouseButton.Middle, (int)events[0].Mouse!.Button, "Button 1 is middle");
                            Check.Equal((int)MouseButton.Right, (int)events[1].Mouse!.Button, "Button 2 is right");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseProtocol", "HoverMove", "Any-motion (1003) hover reports decode as Move with no button",
                        _ =>
                        {
                            IReadOnlyList<InputEvent> events = Parse(Esc + "[<35;10;5M");
                            Check.Equal(1, events.Count, "One event");
                            MouseEvent move = events[0].Mouse!;
                            Check.Equal((int)MouseEventKind.Move, (int)move.Kind, "Kind is move");
                            Check.Equal((int)MouseButton.None, (int)move.Button, "No button held");
                            Check.Equal(9, move.X, "Column");
                            Check.Equal(4, move.Y, "Row");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseProtocol", "DragMove", "Motion with a button held decodes as a drag",
                        _ =>
                        {
                            IReadOnlyList<InputEvent> events = Parse(Esc + "[<32;4;4M" + Esc + "[<34;6;4M");
                            Check.Equal(2, events.Count, "Two events");
                            Check.Equal((int)MouseEventKind.Move, (int)events[0].Mouse!.Kind, "Left drag is move");
                            Check.Equal((int)MouseButton.Left, (int)events[0].Mouse!.Button, "Left button held");
                            Check.Equal((int)MouseButton.Right, (int)events[1].Mouse!.Button, "Right button held");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseProtocol", "WheelAllAxes", "Wheel buttons 64-67 decode to up, down, left, right",
                        _ =>
                        {
                            IReadOnlyList<InputEvent> events = Parse(
                                Esc + "[<64;2;2M" + Esc + "[<65;2;2M" + Esc + "[<66;2;2M" + Esc + "[<67;2;2M");
                            Check.Equal(4, events.Count, "Four wheel events");
                            Check.Equal((int)MouseButton.WheelUp, (int)events[0].Mouse!.Button, "64 is up");
                            Check.Equal((int)MouseButton.WheelDown, (int)events[1].Mouse!.Button, "65 is down");
                            Check.Equal((int)MouseButton.WheelLeft, (int)events[2].Mouse!.Button, "66 is left");
                            Check.Equal((int)MouseButton.WheelRight, (int)events[3].Mouse!.Button, "67 is right");
                            Check.Equal((int)MouseEventKind.Wheel, (int)events[3].Mouse!.Kind, "All are wheel kind");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseProtocol", "Modifiers", "Shift, Alt, and Ctrl bits decode on presses and hover moves",
                        _ =>
                        {
                            IReadOnlyList<InputEvent> events = Parse(
                                Esc + "[<4;1;1M" + Esc + "[<8;1;1M" + Esc + "[<16;1;1M" + Esc + "[<28;1;1M" + Esc + "[<39;1;1M");
                            Check.Equal(5, events.Count, "Five events");
                            Check.Equal((int)KeyModifiers.Shift, (int)events[0].Mouse!.Modifiers, "Bit 4 is shift");
                            Check.Equal((int)KeyModifiers.Alt, (int)events[1].Mouse!.Modifiers, "Bit 8 is alt");
                            Check.Equal((int)KeyModifiers.Ctrl, (int)events[2].Mouse!.Modifiers, "Bit 16 is ctrl");
                            Check.Equal(
                                (int)(KeyModifiers.Shift | KeyModifiers.Alt | KeyModifiers.Ctrl),
                                (int)events[3].Mouse!.Modifiers,
                                "Combined modifier bits");
                            Check.Equal((int)MouseEventKind.Move, (int)events[4].Mouse!.Kind, "35+4 is a shifted hover move");
                            Check.Equal((int)KeyModifiers.Shift, (int)events[4].Mouse!.Modifiers, "Shift on hover move");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseProtocol", "WideCoordinates", "Coordinates beyond column 223 decode exactly",
                        _ =>
                        {
                            IReadOnlyList<InputEvent> events = Parse(Esc + "[<0;500;300M");
                            Check.Equal(1, events.Count, "One event");
                            Check.Equal(499, events[0].Mouse!.X, "Wide column");
                            Check.Equal(299, events[0].Mouse!.Y, "Wide row");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseProtocol", "FocusReports", "CSI I and CSI O decode as focus events",
                        _ =>
                        {
                            IReadOnlyList<InputEvent> events = Parse(Esc + "[I" + Esc + "[O");
                            Check.Equal(2, events.Count, "Two events");
                            Check.Equal((int)InputEventKind.FocusGained, (int)events[0].Kind, "CSI I gains focus");
                            Check.Equal((int)InputEventKind.FocusLost, (int)events[1].Kind, "CSI O loses focus");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseProtocol", "SplitAcrossFeeds", "A report split across two feeds completes on the second",
                        _ =>
                        {
                            InputParser parser = new InputParser();
                            Feed(parser, Esc + "[<0;5");
                            Check.Equal(0, parser.Drain().Count, "Nothing before the final byte");

                            Feed(parser, ";3M");
                            IReadOnlyList<InputEvent> events = parser.Drain();
                            Check.Equal(1, events.Count, "Completed on the second feed");
                            Check.Equal(4, events[0].Mouse!.X, "Coordinates intact");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseProtocol", "EnableSequences", "Enable/disable sequences cover modes 1000-1006",
                        _ =>
                        {
                            Check.True(Ansi.EnableMouse.Contains("?1000h"), "Button tracking enabled");
                            Check.True(Ansi.EnableMouse.Contains("?1002h"), "Drag tracking enabled");
                            Check.True(Ansi.EnableMouse.Contains("?1003h"), "Any-motion enabled");
                            Check.True(Ansi.EnableMouse.Contains("?1006h"), "SGR encoding enabled");
                            Check.True(Ansi.DisableMouse.Contains("?1003l"), "Any-motion disabled");
                            Check.True(Ansi.EnableAnyMotion.Contains("?1003h"), "Standalone any-motion enable");
                            Check.True(Ansi.DisableAnyMotion.Contains("?1003l"), "Standalone any-motion disable");
                            Check.True(Ansi.EnableFocusReporting.Contains("?1004h"), "Focus reporting enable");
                            Check.True(Ansi.DisableFocusReporting.Contains("?1004l"), "Focus reporting disable");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseProtocol", "CapabilityDetection", "Any-motion and focus flags follow the terminal heuristics",
                        _ =>
                        {
                            TerminalCapabilities wt = CapabilityDetector.Detect(
                                name => name == "WT_SESSION" ? "1" : null, true);
                            Check.True(wt.AnyMotionMouse, "Windows Terminal reports any-motion");
                            Check.True(wt.FocusReporting, "Windows Terminal reports focus");

                            TerminalCapabilities screen = CapabilityDetector.Detect(
                                name => name == "TERM" ? "screen" : null, true);
                            Check.False(screen.AnyMotionMouse, "GNU screen degrades to drag-only");
                            Check.False(screen.FocusReporting, "GNU screen has no focus reporting");

                            TerminalCapabilities tmux = CapabilityDetector.Detect(
                                name => name == "TERM" ? "screen-256color" : (name == "TMUX" ? "/tmp/tmux-1000/default,1,0" : null),
                                true);
                            Check.True(tmux.AnyMotionMouse, "tmux passes any-motion despite TERM=screen*");

                            TerminalCapabilities apple = CapabilityDetector.Detect(
                                name => name == "TERM_PROGRAM" ? "Apple_Terminal" : null, true);
                            Check.False(apple.AnyMotionMouse, "Apple Terminal is conservative");

                            TerminalCapabilities headless = CapabilityDetector.Detect(name => null, false);
                            Check.False(headless.AnyMotionMouse, "Non-interactive has no mouse");
                            Check.False(headless.FocusReporting, "Non-interactive has no focus reporting");
                            return Task.CompletedTask;
                        })
                });
        }

        private static IReadOnlyList<InputEvent> Parse(string sequence)
        {
            InputParser parser = new InputParser();
            Feed(parser, sequence);
            return parser.Drain();
        }

        private static void Feed(InputParser parser, string sequence)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(sequence);
            parser.Feed(bytes, bytes.Length);
        }
    }
}

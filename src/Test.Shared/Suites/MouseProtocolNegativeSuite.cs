namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit.Input;

    /// <summary>
    /// Touchstone suite covering malformed, truncated, and hostile SGR mouse input: the parser must
    /// never throw, never emit a bogus event, and always recover for the next valid sequence.
    /// </summary>
    public static class MouseProtocolNegativeSuite
    {
        private const string Esc = "";

        /// <summary>
        /// Builds the negative mouse protocol suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "MouseProtocolNegative",
                displayName: "Mouse Protocol Malformed Input",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("MouseProtocolNegative", "TruncatedPrefixes", "Every truncation of a report emits nothing and stays buffered",
                        _ =>
                        {
                            string full = Esc + "[<0;12;7M";
                            for (int cut = 1; cut < full.Length; cut++)
                            {
                                InputParser parser = new InputParser();
                                Feed(parser, full.Substring(0, cut));
                                Check.Equal(0, CountMouse(parser.Drain()), "No event at prefix length " + cut);

                                Feed(parser, full.Substring(cut));
                                IReadOnlyList<InputEvent> events = parser.Drain();
                                Check.Equal(1, CountMouse(events), "Completes after the rest arrives (cut " + cut + ")");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseProtocolNegative", "NonNumericParams", "Non-numeric parameters are dropped without an event",
                        _ =>
                        {
                            IReadOnlyList<InputEvent> events = Parse(Esc + "[<a;b;cM");
                            Check.Equal(0, CountMouse(events), "Garbage parameters produce no mouse event");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseProtocolNegative", "MissingParams", "A report with too few parameters is dropped",
                        _ =>
                        {
                            Check.Equal(0, CountMouse(Parse(Esc + "[<0;5M")), "Two params dropped");
                            Check.Equal(0, CountMouse(Parse(Esc + "[<0M")), "One param dropped");
                            Check.Equal(0, CountMouse(Parse(Esc + "[<M")), "Empty params dropped");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseProtocolNegative", "ZeroWireCoordinates", "Zero (invalid one-based) coordinates are dropped",
                        _ =>
                        {
                            Check.Equal(0, CountMouse(Parse(Esc + "[<0;0;0M")), "Both zero dropped");
                            Check.Equal(0, CountMouse(Parse(Esc + "[<0;0;5M")), "Zero column dropped");
                            Check.Equal(0, CountMouse(Parse(Esc + "[<0;5;0M")), "Zero row dropped");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseProtocolNegative", "WrongFinalByte", "A final byte other than M/m discards the report",
                        _ =>
                        {
                            Check.Equal(0, CountMouse(Parse(Esc + "[<0;5;3X")), "Final X discarded");
                            Check.Equal(0, CountMouse(Parse(Esc + "[<0;5;3~")), "Final tilde discarded");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseProtocolNegative", "RecoversAfterGarbage", "Valid reports around garbage still decode",
                        _ =>
                        {
                            IReadOnlyList<InputEvent> events = Parse(
                                Esc + "[<0;2;2M" + Esc + "[<zz;;M" + Esc + "[<0;9;9m");
                            Check.Equal(2, CountMouse(events), "Both valid reports decoded");
                            Check.Equal(1, events[0].Mouse == null ? -1 : events[0].Mouse!.X, "First report intact");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseProtocolNegative", "UnknownButtonCode", "An unmapped button code does not throw",
                        _ =>
                        {
                            // Button code 3 without motion is a legacy "release" marker some terminals
                            // emit; it must decode (as MouseButton.None) or drop, never throw.
                            IReadOnlyList<InputEvent> events = Parse(Esc + "[<3;4;4M");
                            foreach (InputEvent inputEvent in events)
                            {
                                if (inputEvent.Mouse != null)
                                    Check.Equal((int)MouseButton.None, (int)inputEvent.Mouse.Button, "Code 3 maps to none");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseProtocolNegative", "ParameterizedFocusFinals", "CSI I/O with parameters are not focus events",
                        _ =>
                        {
                            InputParser parser = new InputParser();
                            Feed(parser, Esc + "[5I");
                            IReadOnlyList<InputEvent> events = parser.Drain();
                            foreach (InputEvent inputEvent in events)
                            {
                                Check.True(
                                    inputEvent.Kind != InputEventKind.FocusGained && inputEvent.Kind != InputEventKind.FocusLost,
                                    "Parameterized final I is not a focus report");
                            }

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

        private static int CountMouse(IReadOnlyList<InputEvent> events)
        {
            int count = 0;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Kind == InputEventKind.Mouse)
                    count++;
            }

            return count;
        }
    }
}

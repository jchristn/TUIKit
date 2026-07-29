namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Input;

    /// <summary>
    /// Validation coverage for input parsing, key-chord parsing, command routing, mouse events, and
    /// the link registry/scanner.
    /// </summary>
    public static class InputRoutingValidationSuite
    {
        /// <summary>
        /// Builds the input/routing validation suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "InputRoutingValidation",
                displayName: "Input, Routing & Links Validation",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("InputRoutingValidation", "KeyChordParse", "KeyChord.Parse reports empty, key-less, and unknown chords",
                        _ =>
                        {
                            Check.Throws<ArgumentException>(() => KeyChord.Parse(""), "empty chord");
                            Check.Throws<ArgumentException>(() => KeyChord.Parse("ctrl"), "modifier with no key");
                            Check.Throws<FormatException>(() => KeyChord.Parse("ctrl+zzz"), "unknown key token");
                            KeyChord ok = KeyChord.Parse("ctrl+shift+a");
                            Check.True(ok.Equals(KeyChord.Parse("ctrl+shift+a")), "valid chord parses stably");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("InputRoutingValidation", "CommandRouting", "Command routing table validates ids and scopes",
                        _ =>
                        {
                            CommandRoutingTable table = new CommandRoutingTable();
                            KeyChord chord = KeyChord.Parse("ctrl+a");
                            Check.Throws<ArgumentException>(() => table.Register(chord, ""), "empty command id");
                            Check.Throws<ArgumentException>(() => table.Register(chord, "cmd", CommandScope.FocusContext, null), "focus scope needs a context");
                            Check.Throws<ArgumentException>(() => table.RegisterSequence(KeyChord.Parse("ctrl+k"), KeyChord.Parse("ctrl+t"), ""), "empty sequence command id");
                            Check.Throws<ArgumentNullException>(() => new CommandRouter(null!), "null routing table");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("InputRoutingValidation", "InputParser", "InputParser.Feed validates its buffer and length",
                        _ =>
                        {
                            InputParser parser = new InputParser();
                            Check.Throws<ArgumentNullException>(() => parser.Feed(null!, 0), "null data");
                            Check.Throws<ArgumentOutOfRangeException>(() => parser.Feed(new byte[2], 5), "length exceeds buffer");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("InputRoutingValidation", "MouseEvent", "MouseEvent round-trips and copies immutably",
                        _ =>
                        {
                            MouseEvent evt = new MouseEvent(MouseEventKind.Press, MouseButton.Left, 3, 4, KeyModifiers.None, 1);
                            Check.Equal(3, evt.X, "x preserved");
                            Check.Equal(4, evt.Y, "y preserved");
                            Check.Equal(1, evt.ClickCount, "click count preserved");

                            MouseEvent doubled = evt.WithClickCount(2);
                            Check.Equal(2, doubled.ClickCount, "copy has new click count");
                            Check.Equal(1, evt.ClickCount, "original unchanged");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("InputRoutingValidation", "Links", "Link registry, scanner, and matches validate arguments",
                        _ =>
                        {
                            LinkRegistry registry = new LinkRegistry();
                            Check.Throws<ArgumentNullException>(() => registry.Add(null!, new Rect(0, 0, 1, 1)), "null link text");
                            Check.Throws<ArgumentNullException>(() => registry.AddRect(null!, new Rect(0, 0, 1, 1)), "null link for rect");
                            Check.Throws<ArgumentException>(() => registry.AddRect(new Link(99, "foreign"), new Rect(0, 0, 1, 1)), "foreign link rejected");

                            Check.Throws<ArgumentNullException>(() => new Link(1, null!), "null link text ctor");
                            Check.Throws<ArgumentNullException>(() => new LinkScanner((IEnumerable<string>)null!), "null scheme prefixes");
                            Check.Throws<ArgumentNullException>(() => new LinkScanner().Scan(null!), "null scan text");
                            Check.Throws<ArgumentNullException>(() => new LinkMatch(0, 1, null!), "null match uri");
                            Check.Throws<ArgumentOutOfRangeException>(() => new ClickSynthesizer().ThresholdMilliseconds = 0, "non-positive click threshold");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}

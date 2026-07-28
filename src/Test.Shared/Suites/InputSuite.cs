namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit.Input;

    /// <summary>
    /// Touchstone suite covering the input decoder, key chords and parsing, and the command routing
    /// table and router (scopes, conflicts, and multi-key sequences). Escape sequences use explicit
    /// escapes so the suite is independent of source-file encoding.
    /// </summary>
    public static class InputSuite
    {
        private const string Esc = "\u001b";

        /// <summary>
        /// Builds the input suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Input",
                displayName: "Input Decoding and Routing",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Input", "ChordParse", "Chords parse and round-trip",
                        _ =>
                        {
                            KeyChord chord = KeyChord.Parse("ctrl+shift+a");
                            Check.Equal(KeyCode.Character, chord.Code, "Code");
                            Check.Equal((int)'a', chord.Rune, "Rune normalized to lower");
                            Check.True((chord.Modifiers & KeyModifiers.Ctrl) != 0, "Ctrl set");
                            Check.True((chord.Modifiers & KeyModifiers.Shift) != 0, "Shift set");
                            Check.Equal(chord, KeyChord.Parse("CTRL+SHIFT+A"), "Case-insensitive");
                            Check.Equal(KeyCode.Enter, KeyChord.Parse("alt+enter").Code, "Named key");
                            Check.Equal(KeyCode.F5, KeyChord.Parse("f5").Code, "Function key");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Input", "DecodePlainAndCtrl", "Decoder handles text and Ctrl keys",
                        _ =>
                        {
                            InputParser parser = new InputParser();
                            Feed(parser, "a");
                            List<InputEvent> events = new List<InputEvent>(parser.Drain());
                            Check.Equal(1, events.Count, "One event for 'a'");
                            Check.Equal((int)'a', events[0].Key.Rune, "Rune a");

                            parser.Feed(new byte[] { 0x03 }, 1); // Ctrl+C
                            events = new List<InputEvent>(parser.Drain());
                            Check.Equal(KeyCode.Character, events[0].Key.Code, "Ctrl+C is a character");
                            Check.Equal((int)'c', events[0].Key.Rune, "Ctrl+C rune");
                            Check.True((events[0].Key.Modifiers & KeyModifiers.Ctrl) != 0, "Ctrl modifier");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Input", "DecodeArrowsAndMods", "Decoder handles CSI arrows with modifiers",
                        _ =>
                        {
                            InputParser parser = new InputParser();
                            Feed(parser, Esc + "[A"); // Up
                            List<InputEvent> events = new List<InputEvent>(parser.Drain());
                            Check.Equal(KeyCode.Up, events[0].Key.Code, "Up arrow");

                            Feed(parser, Esc + "[1;5C"); // Ctrl+Right
                            events = new List<InputEvent>(parser.Drain());
                            Check.Equal(KeyCode.Right, events[0].Key.Code, "Right");
                            Check.True((events[0].Key.Modifiers & KeyModifiers.Ctrl) != 0, "Ctrl+Right");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Input", "DecodeUtf8", "Decoder handles multi-byte UTF-8 across feeds",
                        _ =>
                        {
                            InputParser parser = new InputParser();
                            byte[] bytes = Encoding.UTF8.GetBytes(char.ConvertFromUtf32(0x4E16));
                            parser.Feed(new byte[] { bytes[0] }, 1);
                            Check.Equal(0, new List<InputEvent>(parser.Drain()).Count, "Waits for full sequence");

                            byte[] rest = new byte[bytes.Length - 1];
                            Array.Copy(bytes, 1, rest, 0, rest.Length);
                            parser.Feed(rest, rest.Length);
                            List<InputEvent> events = new List<InputEvent>(parser.Drain());
                            Check.Equal(1, events.Count, "Completed once all bytes present");
                            Check.Equal(0x4E16, events[0].Key.Rune, "Decoded CJK code point");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Input", "BracketedPaste", "Bracketed paste yields a paste event",
                        _ =>
                        {
                            InputParser parser = new InputParser();
                            Feed(parser, Esc + "[200~hello\nworld" + Esc + "[201~");
                            List<InputEvent> events = new List<InputEvent>(parser.Drain());
                            Check.Equal(1, events.Count, "Single paste event");
                            Check.Equal(InputEventKind.Paste, events[0].Kind, "Kind is paste");
                            Check.Equal("hello\nworld", events[0].PasteText, "Paste content");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Input", "EscTimeout", "A lone Escape resolves on flush",
                        _ =>
                        {
                            InputParser parser = new InputParser();
                            parser.Feed(new byte[] { 0x1B }, 1);
                            Check.Equal(0, new List<InputEvent>(parser.Drain()).Count, "Held pending");
                            List<InputEvent> flushed = new List<InputEvent>(parser.Flush());
                            Check.Equal(KeyCode.Escape, flushed[0].Key.Code, "Escape on flush");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Input", "SgrMouse", "Decoder parses an SGR mouse press",
                        _ =>
                        {
                            InputParser parser = new InputParser();
                            Feed(parser, Esc + "[<0;10;5M"); // left press at col 10 row 5 (1-based)
                            List<InputEvent> events = new List<InputEvent>(parser.Drain());
                            Check.Equal(InputEventKind.Mouse, events[0].Kind, "Mouse event");
                            Check.Equal(MouseButton.Left, events[0].Mouse!.Button, "Left button");
                            Check.Equal(9, events[0].Mouse!.X, "Zero-based column");
                            Check.Equal(4, events[0].Mouse!.Y, "Zero-based row");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Input", "RoutingPrecedence", "Focus binding overrides global",
                        _ =>
                        {
                            CommandRoutingTable table = new CommandRoutingTable();
                            table.Register(KeyChord.Parse("ctrl+a"), "global.selectAll");
                            table.Register(KeyChord.Parse("ctrl+a"), "editor.selectLine", CommandScope.FocusContext, "editor");

                            Check.Equal("global.selectAll", table.ResolveSingle(KeyChord.Parse("ctrl+a"), null), "Global when no focus");
                            Check.Equal("editor.selectLine", table.ResolveSingle(KeyChord.Parse("ctrl+a"), "editor"), "Focus overrides global");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Input", "ConflictThrows", "Duplicate registration throws by default",
                        _ =>
                        {
                            CommandRoutingTable table = new CommandRoutingTable();
                            table.Register(KeyChord.Parse("ctrl+s"), "save");
                            Check.Throws<InvalidOperationException>(
                                () => table.Register(KeyChord.Parse("ctrl+s"), "other"), "Conflict");

                            table.ConflictPolicy = ConflictPolicy.LastWins;
                            table.Register(KeyChord.Parse("ctrl+s"), "saveAs");
                            Check.Equal("saveAs", table.ResolveSingle(KeyChord.Parse("ctrl+s"), null), "Last wins");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Input", "MultiKeySequence", "Two-key sequence resolves via the router",
                        _ =>
                        {
                            CommandRoutingTable table = new CommandRoutingTable();
                            table.RegisterSequence(KeyChord.Parse("ctrl+k"), KeyChord.Parse("ctrl+t"), "theme.cycle");
                            CommandRouter router = new CommandRouter(table);

                            CommandResolution first = router.Process(KeyEvent.Char('k', KeyModifiers.Ctrl), null);
                            Check.Equal(CommandResolutionStatus.Pending, first.Status, "First key pending");
                            Check.True(router.HasPending, "Router pending");

                            CommandResolution second = router.Process(KeyEvent.Char('t', KeyModifiers.Ctrl), null);
                            Check.Equal(CommandResolutionStatus.Command, second.Status, "Second key completes");
                            Check.Equal("theme.cycle", second.CommandId, "Resolved command");
                            Check.False(router.HasPending, "Pending cleared");
                            return Task.CompletedTask;
                        })
                });
        }

        private static void Feed(InputParser parser, string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            parser.Feed(bytes, bytes.Length);
        }
    }
}

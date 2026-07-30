namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
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

                    new TestCaseDescriptor("Input", "DecodeSpecialKeys", "Decoder handles Tab, Shift+Tab, and Ctrl letters",
                        _ =>
                        {
                            InputParser parser = new InputParser();

                            parser.Feed(new byte[] { 0x09 }, 1); // Tab
                            List<InputEvent> events = new List<InputEvent>(parser.Drain());
                            Check.Equal(KeyCode.Tab, events[0].Key.Code, "Tab");
                            Check.Equal(KeyModifiers.None, events[0].Key.Modifiers, "Tab has no modifiers");

                            Feed(parser, Esc + "[Z"); // Shift+Tab (CSI Z)
                            events = new List<InputEvent>(parser.Drain());
                            Check.Equal(KeyCode.Tab, events[0].Key.Code, "Shift+Tab code");
                            Check.True((events[0].Key.Modifiers & KeyModifiers.Shift) != 0, "Shift+Tab modifier");

                            parser.Feed(new byte[] { 0x07 }, 1); // Ctrl+G
                            events = new List<InputEvent>(parser.Drain());
                            Check.Equal(KeyCode.Character, events[0].Key.Code, "Ctrl+G is a character");
                            Check.Equal((int)'g', events[0].Key.Rune, "Ctrl+G rune");
                            Check.True((events[0].Key.Modifiers & KeyModifiers.Ctrl) != 0, "Ctrl+G modifier");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Input", "CarriageReturnAndLineFeedAreDistinct", "CR decodes as Enter while LF (Ctrl+J) is a distinct chord",
                        _ =>
                        {
                            InputParser parser = new InputParser();

                            parser.Feed(new byte[] { 0x0D }, 1); // Enter (carriage return)
                            List<InputEvent> events = new List<InputEvent>(parser.Drain());
                            Check.Equal(KeyCode.Enter, events[0].Key.Code, "CR is Enter");
                            Check.Equal(KeyModifiers.None, events[0].Key.Modifiers, "Enter has no modifiers");

                            parser.Feed(new byte[] { 0x0A }, 1); // Ctrl+J (line feed)
                            events = new List<InputEvent>(parser.Drain());
                            Check.Equal(KeyCode.Character, events[0].Key.Code, "LF is a character chord");
                            Check.Equal((int)'j', events[0].Key.Rune, "LF decodes as 'j'");
                            Check.True((events[0].Key.Modifiers & KeyModifiers.Ctrl) != 0, "LF carries Ctrl (Ctrl+J)");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Input", "DecodeFunctionKeys", "Decoder handles F-keys via SS3 and CSI tilde",
                        _ =>
                        {
                            InputParser parser = new InputParser();

                            Feed(parser, Esc + "OP"); // F1 via SS3
                            List<InputEvent> events = new List<InputEvent>(parser.Drain());
                            Check.Equal(KeyCode.F1, events[0].Key.Code, "F1 via SS3");

                            Feed(parser, Esc + "[11~"); // F1 via CSI tilde
                            events = new List<InputEvent>(parser.Drain());
                            Check.Equal(KeyCode.F1, events[0].Key.Code, "F1 via CSI tilde");

                            Feed(parser, Esc + "[15~"); // F5
                            events = new List<InputEvent>(parser.Drain());
                            Check.Equal(KeyCode.F5, events[0].Key.Code, "F5 via CSI tilde");

                            Feed(parser, Esc + "[24~"); // F12
                            events = new List<InputEvent>(parser.Drain());
                            Check.Equal(KeyCode.F12, events[0].Key.Code, "F12 via CSI tilde");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Input", "DecodeNavKeys", "Decoder handles paging and navigation keys",
                        _ =>
                        {
                            InputParser parser = new InputParser();

                            Feed(parser, Esc + "[5~"); // PageUp
                            Check.Equal(KeyCode.PageUp, new List<InputEvent>(parser.Drain())[0].Key.Code, "PageUp");
                            Feed(parser, Esc + "[6~"); // PageDown
                            Check.Equal(KeyCode.PageDown, new List<InputEvent>(parser.Drain())[0].Key.Code, "PageDown");
                            Feed(parser, Esc + "[3~"); // Delete
                            Check.Equal(KeyCode.Delete, new List<InputEvent>(parser.Drain())[0].Key.Code, "Delete");
                            Feed(parser, Esc + "[2~"); // Insert
                            Check.Equal(KeyCode.Insert, new List<InputEvent>(parser.Drain())[0].Key.Code, "Insert");
                            Feed(parser, Esc + "[H"); // Home (CSI final)
                            Check.Equal(KeyCode.Home, new List<InputEvent>(parser.Drain())[0].Key.Code, "Home");
                            Feed(parser, Esc + "[F"); // End (CSI final)
                            Check.Equal(KeyCode.End, new List<InputEvent>(parser.Drain())[0].Key.Code, "End");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Input", "DecodeSs3Arrows", "Decoder handles arrows in application-cursor (SS3) mode",
                        _ =>
                        {
                            InputParser parser = new InputParser();
                            Feed(parser, Esc + "OA");
                            Check.Equal(KeyCode.Up, new List<InputEvent>(parser.Drain())[0].Key.Code, "SS3 Up");
                            Feed(parser, Esc + "OB");
                            Check.Equal(KeyCode.Down, new List<InputEvent>(parser.Drain())[0].Key.Code, "SS3 Down");
                            Feed(parser, Esc + "OC");
                            Check.Equal(KeyCode.Right, new List<InputEvent>(parser.Drain())[0].Key.Code, "SS3 Right");
                            Feed(parser, Esc + "OD");
                            Check.Equal(KeyCode.Left, new List<InputEvent>(parser.Drain())[0].Key.Code, "SS3 Left");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Input", "DecodeCsiFunctionKeys", "Decoder handles F1-F4 in CSI form (Kitty / modified)",
                        _ =>
                        {
                            InputParser parser = new InputParser();

                            Feed(parser, Esc + "[P"); // F1 as bare CSI (Kitty disambiguate mode)
                            List<InputEvent> events = new List<InputEvent>(parser.Drain());
                            Check.Equal(1, events.Count, "One event for CSI F1");
                            Check.Equal(KeyCode.F1, events[0].Key.Code, "F1 via CSI");
                            Check.Equal(KeyModifiers.None, events[0].Key.Modifiers, "Unmodified F1");

                            Feed(parser, Esc + "[1;5P"); // Ctrl+F1
                            events = new List<InputEvent>(parser.Drain());
                            Check.Equal(KeyCode.F1, events[0].Key.Code, "Ctrl+F1 code");
                            Check.True((events[0].Key.Modifiers & KeyModifiers.Ctrl) != 0, "Ctrl+F1 modifier");

                            Feed(parser, Esc + "[S"); // F4
                            Check.Equal(KeyCode.F4, new List<InputEvent>(parser.Drain())[0].Key.Code, "F4 via CSI");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Input", "DecodeCsiCprNotFunctionKey", "A Cursor Position Report is not mistaken for F3",
                        _ =>
                        {
                            InputParser parser = new InputParser();
                            Feed(parser, Esc + "[24;80R"); // CPR: row 24, col 80 — leading param is not 1
                            List<InputEvent> events = new List<InputEvent>(parser.Drain());
                            Check.Equal(0, events.Count, "CPR yields no key event");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Input", "DecodeKittyReleaseSuppressed", "Kitty key-release events are not dispatched as presses",
                        _ =>
                        {
                            InputParser parser = new InputParser();

                            // Press: Ctrl+G reported via CSI u with the ctrl modifier (5 = 1 + Ctrl).
                            Feed(parser, Esc + "[103;5u");
                            List<InputEvent> press = new List<InputEvent>(parser.Drain());
                            Check.Equal(1, press.Count, "Press emits one event");
                            Check.Equal((int)'g', press[0].Key.Rune, "Press rune g");
                            Check.True((press[0].Key.Modifiers & KeyModifiers.Ctrl) != 0, "Press keeps Ctrl despite sub-parameter parsing");

                            // Release: same key with event-type sub-parameter 3. Must be swallowed.
                            Feed(parser, Esc + "[103;5:3u");
                            Check.Equal(0, new List<InputEvent>(parser.Drain()).Count, "Release emits nothing");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Input", "DecodeSplitEscapeSequence", "An escape sequence split across reads decodes once complete",
                        _ =>
                        {
                            InputParser parser = new InputParser();
                            parser.Feed(new byte[] { 0x1B }, 1); // ESC only
                            Check.Equal(0, new List<InputEvent>(parser.Drain()).Count, "ESC alone is pending");
                            parser.Feed(new byte[] { (byte)'[' }, 1); // CSI, still incomplete
                            Check.Equal(0, new List<InputEvent>(parser.Drain()).Count, "CSI without final is pending");
                            parser.Feed(new byte[] { (byte)'A' }, 1); // final byte completes Up
                            List<InputEvent> events = new List<InputEvent>(parser.Drain());
                            Check.Equal(1, events.Count, "Completes once the final byte arrives");
                            Check.Equal(KeyCode.Up, events[0].Key.Code, "Decoded Up");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Input", "LabelAscii", "Chords render as ASCII labels",
                        _ =>
                        {
                            Check.Equal("Ctrl+G", KeyChord.Parse("ctrl+g").ToLabel(KeyLabelStyle.Ascii), "Ctrl+G");
                            Check.Equal("Ctrl+Shift+A", KeyChord.Parse("ctrl+shift+a").ToLabel(KeyLabelStyle.Ascii), "Ctrl+Shift+A");
                            Check.Equal("Alt+Enter", KeyChord.Parse("alt+enter").ToLabel(KeyLabelStyle.Ascii), "Alt+Enter");
                            Check.Equal("F1", KeyChord.Parse("f1").ToLabel(KeyLabelStyle.Ascii), "F1");
                            Check.Equal("PgUp", KeyChord.Parse("pageup").ToLabel(KeyLabelStyle.Ascii), "PgUp");
                            Check.Equal("Up", KeyChord.Parse("up").ToLabel(KeyLabelStyle.Ascii), "Up");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Input", "LabelSymbols", "Chords render as macOS symbol labels",
                        _ =>
                        {
                            Check.Equal("⌃G", KeyChord.Parse("ctrl+g").ToLabel(KeyLabelStyle.Symbols), "Ctrl+G symbols");
                            Check.Equal("⌥⏎", KeyChord.Parse("alt+enter").ToLabel(KeyLabelStyle.Symbols), "Alt+Enter symbols");
                            Check.Equal("⇧⇥", KeyChord.Parse("shift+tab").ToLabel(KeyLabelStyle.Symbols), "Shift+Tab symbols");
                            Check.Equal("⇞", KeyChord.Parse("pageup").ToLabel(KeyLabelStyle.Symbols), "PageUp symbol");
                            Check.Equal("↑", KeyChord.Parse("up").ToLabel(KeyLabelStyle.Symbols), "Up symbol");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Input", "LabelRecommendedMatchesOs", "Recommended label style follows the platform",
                        _ =>
                        {
                            KeyLabelStyle expected = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                                ? KeyLabelStyle.Symbols
                                : KeyLabelStyle.Ascii;
                            Check.Equal(expected, KeyLabel.Recommended, "Recommended style");
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

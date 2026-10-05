namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Hosting;
    using TUIKit.Input;
    using TUIKit.Layout;
    using TUIKit.Terminal;
    using TUIKit.Testing;
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for focus-aware key hints: <see cref="KeyHintResolver"/>, <see cref="KeyHint"/>,
    /// <see cref="IKeyHintSource"/>, <see cref="ITextEntry"/>, <see cref="KeyChord.InsertsTextWhenTyping"/>,
    /// and a <see cref="StatusBar"/> bound with <see cref="TuiApplication.BindKeyHints"/>.
    /// </summary>
    public static class KeyHintsSuite
    {
        /// <summary>
        /// Builds the key hints suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "KeyHints",
                displayName: "Focus-Aware Key Hints",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("KeyHints", "LeafFirstThenApp", "Hints come from the leaf, then its containers, then the application",
                        _ =>
                        {
                            HintLeaf leaf = new HintLeaf { Hints = new[] { new KeyHint("Del", "Delete") } };
                            HintContainer container = new HintContainer(leaf) { Hints = new[] { new KeyHint("F5", "Refresh") } };
                            KeyHintResolver resolver = new KeyHintResolver().AddAppHint("ctrl+q", "Quit");

                            IReadOnlyList<KeyHint> hints = resolver.Resolve(FocusPath.Build("main", container));
                            Check.Equal("Del", hints[0].Key, "leaf first");
                            Check.Equal("F5", hints[1].Key, "container next");
                            Check.Equal("Ctrl+Q", hints[2].Key, "application last");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("KeyHints", "InnermostWins", "When a container and its leaf describe the same key, the leaf's text shows",
                        _ =>
                        {
                            HintLeaf leaf = new HintLeaf { Hints = new[] { new KeyHint("Enter", "Open file") } };
                            HintContainer container = new HintContainer(leaf) { Hints = new[] { new KeyHint("Enter", "Submit form") } };
                            IReadOnlyList<KeyHint> hints = new KeyHintResolver().Resolve(FocusPath.Build("main", container));
                            Check.Equal(1, hints.Count, "one Enter hint");
                            Check.Equal("Open file", hints[0].Description, "leaf description wins");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("KeyHints", "TypingHidesPrintableKeys", "While a text field has focus, typing keys hide and the leave hint leads",
                        _ =>
                        {
                            KeyHintResolver resolver = new KeyHintResolver()
                                .AddAppHint("q", "Quit")
                                .AddAppHint(new KeyHint("?", "Help"))
                                .AddAppHint("shift+a", "Add")
                                .AddAppHint("ctrl+s", "Save")
                                .AddAppHint("f1", "Help");
                            TextField field = new TextField();

                            IReadOnlyList<KeyHint> typing = resolver.Resolve(FocusPath.Build("form", field));
                            Check.Equal("Tab", typing[0].Key, "leave hint first");
                            Check.Equal("Next field", typing[0].Description, "leave hint text");
                            List<string> keys = Keys(typing);
                            Check.False(keys.Contains("Q"), "plain letter hidden");
                            Check.False(keys.Contains("?"), "single printable label hidden");
                            Check.False(keys.Contains("Shift+A"), "shifted letter hidden (it types a capital)");
                            Check.True(keys.Contains("Ctrl+S"), "modified chord kept");
                            Check.True(keys.Contains("F1"), "function key kept");

                            IReadOnlyList<KeyHint> idle = resolver.Resolve(FocusPath.Build("list", new ListView<string>()));
                            Check.True(Keys(idle).Contains("Q") && Keys(idle).Contains("?"), "letters shown when not typing");
                            Check.False(Keys(idle).Contains("Tab"), "no leave hint when not typing");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("KeyHints", "ReadOnlyAndDisabledNotTyping", "Read-only and disabled text widgets report AcceptsText false and get normal hints",
                        _ =>
                        {
                            KeyHintResolver resolver = new KeyHintResolver().AddAppHint("q", "Quit");
                            TextField field = new TextField { IsReadOnly = true };
                            Check.False(field.AcceptsText, "read-only field");
                            Check.True(Keys(resolver.Resolve(FocusPath.Build("f", field))).Contains("Q"), "letters shown over a read-only field");

                            field.IsReadOnly = false;
                            field.IsEnabled = false;
                            Check.False(field.AcceptsText, "disabled field");
                            field.IsEnabled = true;
                            Check.True(field.AcceptsText, "editable field");

                            TextEditor editor = new TextEditor();
                            Check.True(editor.AcceptsText, "editable editor");
                            Check.Equal(2, editor.GetKeyHints()!.Count, "editor offers undo and redo");
                            editor.IsReadOnly = true;
                            Check.False(editor.AcceptsText, "read-only editor");
                            Check.Equal(0, editor.GetKeyHints()!.Count, "no editing hints while read-only");

                            ComboBox combo = new ComboBox(new[] { "a", "b" });
                            Check.True(combo.AcceptsText, "enabled combo box types");
                            combo.IsEnabled = false;
                            Check.False(combo.AcceptsText, "disabled combo box");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("KeyHints", "PriorityAndLimit", "Priority orders hints within a source and MaxHints truncates",
                        _ =>
                        {
                            KeyHintResolver resolver = new KeyHintResolver()
                                .AddAppHint(new KeyHint("A1", "low", 0))
                                .AddAppHint(new KeyHint("A2", "high", 5))
                                .AddAppHint(new KeyHint("A3", "mid", 2));
                            IReadOnlyList<KeyHint> hints = resolver.Resolve(FocusPath.Empty);
                            Check.Equal("A2", hints[0].Key, "highest priority first");
                            Check.Equal("A3", hints[1].Key, "then the middle");
                            Check.Equal("A1", hints[2].Key, "lowest last");

                            resolver.MaxHints = 2;
                            Check.Equal(2, resolver.Resolve(FocusPath.Empty).Count, "truncated to two");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("KeyHints", "CommandsFromRegistry", "Enabled commands with chords become hints; disabled ones do not",
                        _ =>
                        {
                            bool canDelete = false;
                            CommandRegistry registry = new CommandRegistry()
                                .Add(new Command("save", "Save", () => { }, chord: KeyChord.Parse("ctrl+s")))
                                .Add(new Command("delete", "Delete", () => { }, chord: KeyChord.Parse("ctrl+d"), isEnabled: () => canDelete))
                                .Add(new Command("about", "About", () => { }));
                            KeyHintResolver resolver = new KeyHintResolver().AddCommands(registry).AddCommands(registry);

                            List<string> keys = Keys(resolver.Resolve(FocusPath.Empty));
                            Check.Equal(1, keys.Count, "only the enabled command with a chord, registered once");
                            Check.Equal("Ctrl+S", keys[0], "save listed");

                            canDelete = true;
                            Check.True(Keys(resolver.Resolve(FocusPath.Empty)).Contains("Ctrl+D"), "enabling a command updates the hints");
                            resolver.ClearAppHints();
                            Check.Equal(0, resolver.Resolve(FocusPath.Empty).Count, "cleared");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("KeyHints", "WidgetHints", "ListView and DataTable describe their real keys",
                        _ =>
                        {
                            ListView<string> list = new ListView<string>();
                            Check.Equal(0, list.GetKeyHints()!.Count, "empty list offers nothing");
                            list.SetItems(new[] { "a" });
                            Check.False(Keys(list.GetKeyHints()!).Contains("Enter"), "no Enter without an activation handler");
                            list.ItemActivated += _ => { };
                            Check.Equal("Enter", list.GetKeyHints()![0].Key, "Enter first once activation is handled");
                            list.IsEnabled = false;
                            Check.Equal(0, list.GetKeyHints()!.Count, "disabled list offers nothing");

                            DataTable<string> table = new DataTable<string>();
                            table.Column("Name", s => s);
                            table.Bind(new[] { "x", "y" });
                            table.MultiSelect = true;
                            List<string> keys = Keys(table.GetKeyHints()!);
                            Check.True(keys.Contains("Space") && keys.Contains("Ctrl+A"), "marking keys with multi-select");
                            Check.False(keys.Contains("Esc"), "no clear-marks hint with nothing marked");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("KeyHints", "BoundStatusBarFollowsFocus", "A bound status bar shows the focused widget's keys, then the pinned ones",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(80, 6);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                ListView<string> list = new ListView<string>();
                                list.SetItems(new[] { "one", "two" });
                                list.ItemActivated += _ => { };
                                StatusBar bar = new StatusBar().Add("F10", "Menu");
                                app.Layout = Layout.Create()
                                    .Add("list", r => r.ProportionalWidth(0, 0.5).TopAnchored(0, 5).WithPadding(0))
                                    .Add("field", r => r.ProportionalWidth(0.5, 0.5).TopAnchored(0, 5).WithPadding(0))
                                    .Add("status", r => r.FillWidth().BottomAnchored(0, 1).WithPadding(0))
                                    .Build();
                                app.Bind("list", list);
                                app.Bind("field", new TextField());
                                app.Bind("status", bar);
                                KeyHintResolver resolver = app.BindKeyHints(bar);
                                resolver.AddAppHint("q", "Quit");
                                app.Start();

                                string status = StatusRow(app);
                                Check.True(status.StartsWith("Enter Open", StringComparison.Ordinal), "list keys lead: " + status);
                                Check.True(status.Contains("Q Quit"), "app hint shown");
                                Check.True(status.TrimEnd().EndsWith("F10 Menu", StringComparison.Ordinal), "pinned hint last");

                                app.FocusNext();
                                status = StatusRow(app);
                                Check.True(status.StartsWith("Tab Next field", StringComparison.Ordinal), "leave hint leads in the field: " + status);
                                Check.False(status.Contains("Q Quit"), "typing key hidden in the field");
                                Check.True(status.Contains("F10 Menu"), "pinned hint still shown");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("KeyHints", "InsertsTextTable", "InsertsTextWhenTyping over letters, digits, punctuation, Space, modifiers, and special keys",
                        _ =>
                        {
                            string[] typing = { "a", "z", "shift+a", "5", "?", "/", "space" };
                            string[] notTyping = { "ctrl+a", "alt+x", "super+c", "f1", "f12", "escape", "enter", "tab", "up", "down", "left", "right", "backspace", "ctrl+shift+z" };
                            foreach (string chord in typing)
                                Check.True(KeyChord.Parse(chord).InsertsTextWhenTyping(), chord + " types");
                            foreach (string chord in notTyping)
                                Check.False(KeyChord.Parse(chord).InsertsTextWhenTyping(), chord + " does not type");
                            Check.False(new KeyChord(KeyCode.Character, 0, KeyModifiers.None).InsertsTextWhenTyping(), "empty character does not type");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("KeyHints", "PlaceholderUnderCaret", "A focused empty field keeps its placeholder's first letter under the caret",
                        _ =>
                        {
                            TextField field = new TextField { Placeholder = "Search", IsFocused = true };
                            WidgetTester tester = WidgetTester.For(field, 20, 1).Render();
                            Check.Equal("S", tester.CellAt(0, 0).Grapheme, "first letter shown under the caret");
                            Check.True((tester.CellAt(0, 0).Style.Attributes & CellAttributes.Reverse) != 0, "caret drawn in reverse video");
                            Check.Equal("Search", tester.Row(0), "whole placeholder visible");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("KeyHints", "Guards", "Invalid hints, limits, and arguments are rejected; null sources are empty",
                        _ =>
                        {
                            KeyHintResolver resolver = new KeyHintResolver();
                            Check.Throws<ArgumentOutOfRangeException>(() => resolver.MaxHints = 0, "MaxHints below 1");
                            Check.Throws<ArgumentOutOfRangeException>(() => resolver.MaxHints = 65, "MaxHints above 64");
                            Check.Throws<ArgumentNullException>(() => resolver.LeaveTextHint = null!, "null leave hint");
                            Check.Throws<ArgumentNullException>(() => resolver.Resolve(null!), "null path");
                            Check.Throws<ArgumentNullException>(() => resolver.AddAppHint((KeyHint)null!), "null app hint");
                            Check.Throws<ArgumentNullException>(() => resolver.AddCommands(null!), "null registry");
                            Check.Throws<ArgumentNullException>(() => KeyHintResolver.WouldType(null!), "null hint");
                            Check.Throws<ArgumentException>(() => new KeyHint(string.Empty, "x"), "empty key");
                            Check.Throws<ArgumentException>(() => new KeyHint(null!, "x"), "null key");
                            Check.Throws<ArgumentNullException>(() => new KeyHint("k", null!), "null description");
                            Check.Throws<ArgumentException>(() => new KeyHint(new KeyChord(KeyCode.Character, 0, KeyModifiers.None), "x"), "chord without a label");

                            using (TuiApplication app = new TuiApplication(new HeadlessBackend(10, 4)))
                                Check.Throws<ArgumentNullException>(() => app.BindKeyHints(null!), "null status bar");

                            HintLeaf silent = new HintLeaf { Hints = null };
                            HintContainer container = new HintContainer(silent) { Hints = new KeyHint[] { null!, new KeyHint("F2", "Rename") } };
                            IReadOnlyList<KeyHint> hints = resolver.Resolve(FocusPath.Build("x", container));
                            Check.Equal(1, hints.Count, "null source and null entries are skipped");
                            Check.Equal("F2", hints[0].Key, "remaining hint kept");

                            StatusBar bar = new StatusBar { HintSource = () => new KeyHint[] { null!, new KeyHint("F3", "Find") } };
                            WidgetTester.For(bar, 30, 1).Render().AssertContains("F3 Find");
                            return Task.CompletedTask;
                        })
                });
        }

        private static List<string> Keys(IReadOnlyList<KeyHint> hints)
        {
            List<string> keys = new List<string>();
            foreach (KeyHint hint in hints)
                keys.Add(hint.Key);
            return keys;
        }

        private static string StatusRow(TuiApplication app)
        {
            app.RenderOnce();
            string[] rows = Snapshot.ToText(app.CaptureFrame()!).Split('\n');
            return rows[rows.Length - 1];
        }
    }
}

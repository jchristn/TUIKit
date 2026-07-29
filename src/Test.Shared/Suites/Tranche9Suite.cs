namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit.Input;
    using TUIKit.Testing;
    using TUIKit.Widgets;

    /// <summary>
    /// Touchstone suite for tranche 9: the editable key-binding set and the key-binding editor widget
    /// (the user-configurable keymap).
    /// </summary>
    public static class Tranche9Suite
    {
        /// <summary>
        /// Builds the tranche 9 suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Tranche9",
                displayName: "Tranche 9 (Key Bindings)",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Tranche9", "BindingSet", "Binding set rebinds and rejects conflicts",
                        _ =>
                        {
                            KeyBindingSet set = new KeyBindingSet()
                                .Add("save", "ctrl+s")
                                .Add("quit", "ctrl+q")
                                .Add("find", "ctrl+f");

                            Check.True(set.Find("save")!.Chord.Equals(KeyChord.Parse("ctrl+s")), "save bound to ctrl+s");
                            Check.Equal("quit", set.CommandFor(KeyChord.Parse("ctrl+q")), "reverse lookup");

                            Check.True(set.Rebind("save", KeyChord.Parse("ctrl+w")), "rebind succeeds");
                            Check.True(set.Find("save")!.Chord.Equals(KeyChord.Parse("ctrl+w")), "new chord stored");

                            Check.False(set.Rebind("save", KeyChord.Parse("ctrl+q")), "conflict rejected");
                            Check.True(set.Find("save")!.Chord.Equals(KeyChord.Parse("ctrl+w")), "chord unchanged after conflict");
                            Check.False(set.Rebind("nope", KeyChord.Parse("ctrl+z")), "unknown command rejected");

                            Check.Throws<ArgumentNullException>(() => set.Add(null!, "ctrl+a"), "null command");
                            Check.Throws<ArgumentNullException>(() => set.Add("cmd", (string)null!), "null chord text");
                            Check.Throws<ArgumentNullException>(() => set.Find(null!), "null find");
                            Check.Throws<ArgumentNullException>(() => set.Rebind(null!, KeyChord.Parse("a")), "null rebind command");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Tranche9", "BindingEditor", "Binding editor captures and rebinds keys",
                        _ =>
                        {
                            KeyBindingSet set = new KeyBindingSet().Add("save", "s").Add("quit", "q");
                            KeyBindingEditor editor = new KeyBindingEditor(set);
                            Check.False(editor.IsCapturing, "not capturing initially");
                            Check.Equal(0, editor.SelectedIndex, "first row selected");

                            Check.True(editor.HandleKey(KeyEvent.Special(KeyCode.Down)), "down consumed");
                            Check.Equal(1, editor.SelectedIndex, "moved down");
                            editor.HandleKey(KeyEvent.Special(KeyCode.Up));
                            Check.Equal(0, editor.SelectedIndex, "moved up");
                            Check.False(editor.HandleKey(KeyEvent.Char('z')), "unrelated key ignored when idle");

                            editor.HandleKey(KeyEvent.Special(KeyCode.Enter));
                            Check.True(editor.IsCapturing, "capturing after enter");
                            editor.HandleKey(KeyEvent.Char('x'));
                            Check.False(editor.IsCapturing, "capture ends after a key");
                            Check.True(editor.LastError == null, "no error on clean rebind");
                            Check.True(set.Find("save")!.Chord.Equals(KeyChord.FromKeyEvent(KeyEvent.Char('x'))), "save rebound to x");

                            editor.HandleKey(KeyEvent.Special(KeyCode.Enter));
                            editor.HandleKey(KeyEvent.Char('q')); // conflicts with quit
                            Check.True(editor.LastError != null, "conflict reported");
                            Check.True(set.Find("save")!.Chord.Equals(KeyChord.FromKeyEvent(KeyEvent.Char('x'))), "save chord unchanged on conflict");

                            editor.HandleKey(KeyEvent.Special(KeyCode.Enter));
                            Check.True(editor.IsCapturing, "capturing again");
                            editor.HandleKey(KeyEvent.Special(KeyCode.Escape));
                            Check.False(editor.IsCapturing, "escape cancels capture");
                            Check.True(editor.LastError == null, "escape clears error");

                            WidgetTester tester = WidgetTester.For(editor, 30, 4).Render();
                            tester.AssertContains("save").AssertContains("quit");

                            Check.Throws<ArgumentNullException>(() => new KeyBindingEditor(null!), "null set");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}

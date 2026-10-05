namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Testing;
    using TUIKit.Widgets;

    /// <summary>
    /// Touchstone suite covering widget change events (U2), programmatic sets raising them, and the
    /// disabled and read-only states of inputs and pickers.
    /// </summary>
    public static class ChangeEventsSuite
    {
        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "ChangeEvents",
                displayName: "Widget Change Events and Disabled State",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("ChangeEvents", "ListView", "ListView raises SelectionChanged for keys, mouse, and programmatic changes",
                        _ =>
                        {
                            ListView<string> list = new ListView<string>();
                            List<string> seen = new List<string>();
                            list.SelectionChanged += (s, e) => seen.Add(e.OldValue + ">" + e.NewValue);
                            list.SetItems(new[] { "a", "b", "c" });
                            list.HandleKey(KeyEvent.Special(KeyCode.Down));
                            list.Select(2);
                            list.Select(2);
                            Snapshot.RenderWidget(list, 10, 3);
                            list.HandleMouse(new MouseEvent(MouseEventKind.Press, MouseButton.Left, 0, 0, KeyModifiers.None, 1));
                            Check.Equal("-1>0,0>1,1>2,2>0", string.Join(",", seen), "events in order, none for a no-op select");
                            list.SetItems(new[] { "x", "y" });
                            Check.Equal(5, seen.Count, "replacing items at the same index still reports a change");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ChangeEvents", "ListViewActivated", "ListView raises ItemActivated on Enter only when handled",
                        _ =>
                        {
                            ListView<string> list = new ListView<string>();
                            list.SetItems(new[] { "a", "b" });
                            Check.False(list.HandleKey(KeyEvent.Special(KeyCode.Enter)), "Enter falls through without a handler");
                            int activated = -1;
                            list.ItemActivated += i => activated = i;
                            list.SelectNext();
                            Check.True(list.HandleKey(KeyEvent.Special(KeyCode.Enter)), "Enter consumed with a handler");
                            Check.Equal(1, activated, "activated index");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ChangeEvents", "TextField", "TextField raises ValueChanged for typing, paste, and programmatic sets",
                        _ =>
                        {
                            TextField field = new TextField();
                            List<string> values = new List<string>();
                            field.ValueChanged += (s, e) => values.Add(e.NewValue);
                            int untyped = 0;
                            field.Changed += (s, e) => untyped++;
                            field.HandleKey(KeyEvent.Char('a'));
                            field.Insert("bc");
                            field.Value = "xyz";
                            field.Value = "xyz";
                            field.HandleKey(KeyEvent.Special(KeyCode.Backspace));
                            Check.Equal("a,abc,xyz,xy", string.Join(",", values), "every change, once");
                            Check.Equal(4, untyped, "untyped event mirrors the typed one");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ChangeEvents", "TextFieldReadOnlyDisabled", "Read-only fields refuse edits but move the caret; disabled fields ignore input",
                        _ =>
                        {
                            TextField field = new TextField { Value = "abc", IsReadOnly = true };
                            Check.False(field.HandleKey(KeyEvent.Char('x')), "typing not consumed");
                            Check.False(field.HandleKey(KeyEvent.Special(KeyCode.Backspace)), "backspace not consumed");
                            Check.True(field.HandleKey(KeyEvent.Special(KeyCode.Left)), "caret keys work");
                            field.Insert("zz");
                            Check.Equal("abc", field.Value, "paste refused");
                            field.Value = "set";
                            Check.Equal("set", field.Value, "programmatic set still applies");
                            field.IsReadOnly = false;
                            field.IsEnabled = false;
                            Check.False(field.HandleKey(KeyEvent.Special(KeyCode.Left)), "disabled ignores every key");
                            Check.False(field.HandleMouse(new MouseEvent(MouseEventKind.Press, MouseButton.Left, 0, 0, KeyModifiers.None, 1)), "and the mouse");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ChangeEvents", "TextFieldScrollsCjk", "TextField scrolls horizontally and measures CJK by cell width",
                        _ =>
                        {
                            TextField field = new TextField { IsFocused = true };
                            field.Value = "\u4E2D\u6587\u5B57\u7B26\u6D4B\u8BD5";
                            string row = Snapshot.RenderWidget(field, 6, 1);
                            Check.True(field.ScrollColumn > 0, "scrolled to keep the caret visible");
                            Check.True(row.Contains("\u8BD5"), "the end of the value is visible");
                            field.HandleKey(KeyEvent.Special(KeyCode.Home));
                            row = Snapshot.RenderWidget(field, 6, 1);
                            Check.Equal(0, field.ScrollColumn, "Home scrolls back");
                            Check.True(row.Contains("\u4E2D"), "start visible");
                            field.HandleKey(KeyEvent.Special(KeyCode.Right));
                            Check.Equal(1, field.CaretIndex, "Right steps one wide glyph");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ChangeEvents", "TextFieldGraphemes", "TextField deletes whole grapheme clusters",
                        _ =>
                        {
                            TextField field = new TextField();
                            field.Value = "a\U0001F600";
                            field.HandleKey(KeyEvent.Special(KeyCode.Backspace));
                            Check.Equal("a", field.Value, "the surrogate pair is removed together");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ChangeEvents", "TextEditor", "TextEditor raises TextChanged once per edit and on programmatic sets",
                        _ =>
                        {
                            TextEditor editor = new TextEditor();
                            int count = 0;
                            editor.TextChanged += (s, e) => count++;
                            editor.Text = "one";
                            editor.Text = "one";
                            editor.InsertText("a\nb");
                            editor.HandleKey(KeyEvent.Special(KeyCode.Backspace));
                            editor.Undo();
                            editor.HandleKey(KeyEvent.Special(KeyCode.Left));
                            Check.Equal(4, count, "set, insert, backspace, undo; no event for a no-op set or caret move");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ChangeEvents", "TextEditorControlKeys", "TextEditor can pass unbound Ctrl chords through",
                        _ =>
                        {
                            TextEditor editor = new TextEditor();
                            Check.True(editor.HandleKey(KeyEvent.Char('p', KeyModifiers.Ctrl)), "default swallows unbound chords");
                            editor.ConsumeUnboundControlKeys = false;
                            Check.False(editor.HandleKey(KeyEvent.Char('p', KeyModifiers.Ctrl)), "pass-through when configured");
                            Check.True(editor.HandleKey(KeyEvent.Char('z', KeyModifiers.Ctrl)), "bound chords still handled");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ChangeEvents", "TextEditorHorizontalScroll", "TextEditor scrolls long lines horizontally and handles wide glyphs",
                        _ =>
                        {
                            TextEditor editor = new TextEditor { IsFocused = true };
                            editor.Text = "0123456789abcdefghij";
                            string text = Snapshot.RenderWidget(editor, 8, 2);
                            Check.True(editor.ScrollColumn > 0, "scrolled to the caret");
                            Check.True(text.Contains("j"), "line end visible");
                            Check.False(text.Contains("0123"), "line start scrolled off");
                            editor.Text = "\u4E2D\u6587";
                            Snapshot.RenderWidget(editor, 8, 2);
                            editor.MoveLeft();
                            Check.Equal(1, editor.CaretColumn, "caret steps one wide glyph");
                            editor.IsReadOnly = true;
                            Check.False(editor.HandleKey(KeyEvent.Char('x')), "read-only refuses typing");
                            Check.True(editor.HandleKey(KeyEvent.Special(KeyCode.Left)), "read-only still navigates");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ChangeEvents", "TextEditorWrapWidth", "Word wrap measures cells, so CJK lines wrap at the width",
                        _ =>
                        {
                            TextEditor editor = new TextEditor { WordWrap = true };
                            editor.Text = "\u4E2D\u6587\u5B57\u7B26";
                            Check.Equal(2, editor.VisualLineCount(4), "eight columns wrap into two four-column rows");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ChangeEvents", "CheckboxAndRadio", "Checkbox and RadioGroup raise events and honor IsEnabled",
                        _ =>
                        {
                            Checkbox box = new Checkbox("x");
                            List<bool> checks = new List<bool>();
                            box.CheckedChanged += (s, e) => checks.Add(e.NewValue);
                            box.HandleKey(KeyEvent.Char(' '));
                            box.Checked = true;
                            box.Checked = false;
                            Check.Equal("True,False", string.Join(",", checks), "key and programmatic toggles");
                            box.IsEnabled = false;
                            Check.False(box.HandleKey(KeyEvent.Char(' ')), "disabled ignores Space");

                            RadioGroup radio = new RadioGroup(new[] { "a", "b", "c" });
                            List<int> picks = new List<int>();
                            radio.SelectionChanged += (s, e) => picks.Add(e.NewValue);
                            radio.HandleKey(KeyEvent.Special(KeyCode.Down));
                            radio.SelectedIndex = 2;
                            radio.SelectedIndex = 9;
                            Check.Equal("1,2", string.Join(",", picks), "clamped set raises nothing new");
                            radio.IsEnabled = false;
                            Check.False(radio.HandleKey(KeyEvent.Special(KeyCode.Up)), "disabled ignores keys");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}

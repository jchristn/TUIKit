namespace Test.Shared.Suites
{
    using System;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Input;
    using TUIKit.Widgets;

    /// <summary>
    /// Touchstone suite covering the form-input widgets (text field, checkbox, radio group) and the
    /// text-search edge cases of the editor and pane, with positive and negative cases.
    /// </summary>
    public static class InputWidgetSuite
    {
        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "InputWidget",
                displayName: "Input Widgets & Search",
                cases: new System.Collections.Generic.List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("InputWidget", "TextField", "Text field edits, rejects null, and ignores control keys",
                        _ =>
                        {
                            TextField field = new TextField();
                            Check.Equal(string.Empty, field.Value, "empty by default");
                            field.HandleKey(KeyEvent.Char('h'));
                            field.HandleKey(KeyEvent.Char('i'));
                            Check.Equal("hi", field.Value, "typed characters appended");
                            field.HandleKey(KeyEvent.Special(KeyCode.Backspace));
                            Check.Equal("h", field.Value, "backspace removed a character");

                            Check.False(field.HandleKey(KeyEvent.Char('c', KeyModifiers.Ctrl)), "ctrl+char not consumed");
                            Check.Equal("h", field.Value, "ctrl+char did not edit");

                            field.Value = "reset";
                            Check.Equal("reset", field.Value, "value set directly");
                            Check.Throws<ArgumentNullException>(() => field.Value = null!, "null value rejected");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("InputWidget", "Checkbox", "Checkbox toggles and validates its label",
                        _ =>
                        {
                            Checkbox box = new Checkbox("agree");
                            Check.False(box.Checked, "unchecked by default");
                            Check.True(box.HandleKey(KeyEvent.Char(' ')), "space consumed");
                            Check.True(box.Checked, "space toggled on");
                            box.HandleKey(KeyEvent.Special(KeyCode.Enter));
                            Check.False(box.Checked, "enter toggled off");
                            Check.False(box.HandleKey(KeyEvent.Char('x')), "other key ignored");

                            Checkbox preset = new Checkbox("on", true);
                            Check.True(preset.Checked, "initial checked state honored");
                            Check.Throws<ArgumentNullException>(() => new Checkbox(null!), "null label rejected");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("InputWidget", "RadioGroup", "Radio group clamps selection and validates options",
                        _ =>
                        {
                            RadioGroup group = new RadioGroup(new[] { "low", "medium", "high" });
                            Check.Equal(0, group.SelectedIndex, "first option selected");
                            Check.Equal("low", group.SelectedOption, "selected option text");

                            group.HandleKey(KeyEvent.Special(KeyCode.Up)); // clamps at 0
                            Check.Equal(0, group.SelectedIndex, "up at top stays at 0");
                            group.HandleKey(KeyEvent.Special(KeyCode.Down));
                            group.HandleKey(KeyEvent.Special(KeyCode.Down));
                            group.HandleKey(KeyEvent.Special(KeyCode.Down)); // clamps at last
                            Check.Equal(2, group.SelectedIndex, "down clamps at last option");
                            Check.Equal("high", group.SelectedOption, "last option text");

                            Check.Throws<ArgumentNullException>(() => new RadioGroup(null!), "null options");
                            Check.Throws<ArgumentException>(() => new RadioGroup(Array.Empty<string>()), "empty options");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("InputWidget", "EditorFind", "Editor find and replace handle misses and null arguments",
                        _ =>
                        {
                            TextEditor editor = new TextEditor();
                            editor.Text = "hello world\nhello there";

                            Check.True(editor.Find("hello"), "existing term found");
                            Check.False(editor.Find("zzz"), "missing term not found");
                            Check.False(editor.Find(null!), "null query returns false");
                            Check.False(editor.Find(string.Empty), "empty query returns false");

                            Check.Equal(2, editor.ReplaceAll("hello", "hi"), "replaced both occurrences");
                            Check.Equal(0, editor.ReplaceAll("nomatch", "x"), "no matches replaced");
                            Check.Equal(0, editor.ReplaceAll(null!, "x"), "null find replaces nothing");
                            Check.Throws<ArgumentNullException>(() => editor.ReplaceAll("hi", null!), "null replacement rejected");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("InputWidget", "PaneSearch", "Pane search finds matches and clears on null",
                        _ =>
                        {
                            Pane pane = new Pane("p");
                            pane.WriteLine("alpha one");
                            pane.WriteLine("beta two");
                            pane.WriteLine("alpha three");

                            CellBuffer buffer = new CellBuffer(20, 5);
                            pane.Render(new BufferSurface(buffer));

                            pane.SetSearch("alpha");
                            Check.True(pane.FindNext(), "finds a forward match");
                            Check.True(pane.FindPrevious(), "finds a backward match");

                            pane.SetSearch("zzz");
                            Check.False(pane.FindNext(), "no match for a missing term");

                            pane.SetSearch(null);
                            Check.False(pane.FindNext(), "cleared search finds nothing");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}

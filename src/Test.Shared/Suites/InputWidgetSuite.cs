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

                    new TestCaseDescriptor("InputWidget", "TextFieldInsert", "Text field inserts pasted text at the caret and strips control characters",
                        _ =>
                        {
                            TextField field = new TextField();

                            // Insert at the end of an empty field.
                            field.Insert("hello");
                            Check.Equal("hello", field.Value, "pasted text inserted");

                            // Insert at the caret, not just at the end: move to the start and paste there.
                            field.HandleKey(KeyEvent.Special(KeyCode.Home));
                            field.Insert("say ");
                            Check.Equal("say hello", field.Value, "pasted text inserted at the caret");

                            // The caret advanced past the inserted run, so typing lands mid-string.
                            field.HandleKey(KeyEvent.Char('X'));
                            Check.Equal("say Xhello", field.Value, "caret sits after the inserted text");

                            // Newlines, tabs, and other control characters are dropped so a multi-line or
                            // newline-terminated clipboard payload collapses onto the single line.
                            TextField secret = new TextField();
                            secret.Insert("AKIA\r\nEXAMPLE\tKEY\n");
                            Check.Equal("AKIAEXAMPLEKEY", secret.Value, "control characters stripped from paste");

                            // Null and empty are no-ops.
                            secret.Insert(null);
                            secret.Insert(string.Empty);
                            Check.Equal("AKIAEXAMPLEKEY", secret.Value, "null and empty paste change nothing");

                            // A paste that is entirely control characters leaves the value untouched.
                            secret.Insert("\r\n\t");
                            Check.Equal("AKIAEXAMPLEKEY", secret.Value, "all-control paste is a no-op");

                            // Non-ASCII printable characters survive the sanitizer.
                            TextField unicode = new TextField();
                            unicode.Insert("café—π");
                            Check.Equal("café—π", unicode.Value, "printable unicode preserved");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("InputWidget", "TextFieldMask", "Text field masks its rendered value but keeps the real value and editing",
                        _ =>
                        {
                            TextField field = new TextField();
                            Check.False(field.IsMasked, "not masked by default");

                            field.MaskChar = '•';
                            Check.True(field.IsMasked, "masked once a mask char is set");

                            field.HandleKey(KeyEvent.Char('s'));
                            field.HandleKey(KeyEvent.Char('3'));
                            field.HandleKey(KeyEvent.Char('c'));
                            Check.Equal("s3c", field.Value, "underlying value is the typed text, not the mask");

                            CellBuffer buffer = new CellBuffer(8, 1);
                            field.Render(new BufferSurface(buffer));
                            Check.Equal("•", buffer.Get(0, 0).Grapheme, "first cell rendered as the mask char");
                            Check.Equal("•", buffer.Get(1, 0).Grapheme, "second cell rendered as the mask char");
                            Check.Equal("•", buffer.Get(2, 0).Grapheme, "third cell rendered as the mask char");

                            field.MaskChar = '\0';
                            Check.False(field.IsMasked, "clearing the mask char disables masking");
                            CellBuffer plain = new CellBuffer(8, 1);
                            field.Render(new BufferSurface(plain));
                            Check.Equal("s", plain.Get(0, 0).Grapheme, "value renders in clear once unmasked");
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

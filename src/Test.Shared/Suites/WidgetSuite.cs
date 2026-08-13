namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Widgets;

    /// <summary>
    /// Touchstone suite covering the widget toolkit: list navigation, gauge and sparkline rendering,
    /// the multi-line editor (typing, newline, backspace, undo/redo, kill/yank), text field, and
    /// checkbox.
    /// </summary>
    public static class WidgetSuite
    {
        /// <summary>
        /// Builds the widget suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Widget",
                displayName: "Widget Toolkit",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Widget", "ListNavigation", "List selection moves and clamps",
                        _ =>
                        {
                            ListView<string> list = new ListView<string>();
                            list.SetItems(new[] { "a", "b", "c" });
                            Check.Equal(0, list.SelectedIndex, "Starts at first");
                            list.HandleKey(KeyEvent.Special(KeyCode.Down));
                            list.HandleKey(KeyEvent.Special(KeyCode.Down));
                            Check.Equal(2, list.SelectedIndex, "Moved to last");
                            list.HandleKey(KeyEvent.Special(KeyCode.Down));
                            Check.Equal(2, list.SelectedIndex, "Clamped at last");
                            Check.Equal("c", list.SelectedItem, "Selected item");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Widget", "GaugeFill", "Gauge fills proportionally",
                        _ =>
                        {
                            Gauge gauge = new Gauge();
                            gauge.Value = 0.5;
                            CellBuffer buffer = new CellBuffer(10, 1);
                            gauge.Render(new BufferSurface(buffer));
                            int filled = 0;
                            for (int x = 0; x < 10; x++)
                            {
                                if (buffer.Get(x, 0).Grapheme == "█")
                                    filled++;
                            }

                            Check.Equal(5, filled, "Half filled");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Widget", "SparklineRender", "Sparkline renders one glyph per value",
                        _ =>
                        {
                            Sparkline spark = new Sparkline();
                            spark.SetValues(new double[] { 1, 2, 3, 4 });
                            CellBuffer buffer = new CellBuffer(10, 1);
                            spark.Render(new BufferSurface(buffer));
                            Check.False(buffer.Get(0, 0).Grapheme == " ", "First column drawn");
                            Check.False(buffer.Get(3, 0).Grapheme == " ", "Fourth column drawn");
                            Check.True(buffer.Get(4, 0).Grapheme == " ", "No fifth value");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Widget", "EditorTyping", "Editor inserts, newlines, and backspaces",
                        _ =>
                        {
                            TextEditor editor = new TextEditor();
                            Type(editor, "hello");
                            editor.HandleKey(KeyEvent.Special(KeyCode.Enter));
                            Type(editor, "world");
                            Check.Equal("hello\nworld", editor.Text, "Two lines typed");
                            Check.Equal(1, editor.CaretRow, "Caret on second line");

                            editor.HandleKey(KeyEvent.Special(KeyCode.Backspace));
                            Check.Equal("hello\nworl", editor.Text, "Backspace removed last char");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Widget", "EditorUndoRedo", "Editor undo and redo restore state",
                        _ =>
                        {
                            TextEditor editor = new TextEditor();
                            Type(editor, "abc");
                            editor.Undo();
                            Check.Equal("ab", editor.Text, "Undo removed last insert");
                            editor.Redo();
                            Check.Equal("abc", editor.Text, "Redo restored");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Widget", "EditorKillYank", "Kill to end of line and yank",
                        _ =>
                        {
                            TextEditor editor = new TextEditor();
                            editor.Text = "hello world";
                            for (int i = 0; i < 6; i++)
                                editor.MoveLeft(); // caret before 'world'... move to position 5
                            editor.KillToEndOfLine();
                            Check.Equal("hello", editor.Text, "Killed to end");
                            editor.Yank();
                            Check.Equal("hello world", editor.Text, "Yanked back");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Widget", "TextFieldEdit", "Text field edits value",
                        _ =>
                        {
                            TextField field = new TextField();
                            field.HandleKey(KeyEvent.Char('h'));
                            field.HandleKey(KeyEvent.Char('i'));
                            Check.Equal("hi", field.Value, "Typed value");
                            field.HandleKey(KeyEvent.Special(KeyCode.Backspace));
                            Check.Equal("h", field.Value, "Backspaced");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Widget", "CheckboxToggle", "Checkbox toggles on space",
                        _ =>
                        {
                            Checkbox box = new Checkbox("enabled");
                            Check.False(box.Checked, "Starts unchecked");
                            box.HandleKey(KeyEvent.Char(' '));
                            Check.True(box.Checked, "Toggled on");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Widget", "TableRender", "Table renders headers and rows",
                        _ =>
                        {
                            Table table = new Table(new[] { "Name", "Value" });
                            table.AddRow(new[] { "cpu", "42" });
                            CellBuffer buffer = new CellBuffer(20, 3);
                            table.Render(new BufferSurface(buffer));
                            Check.True(ReadRow(buffer, 0).Contains("Name"), "Header rendered");
                            Check.True(ReadRow(buffer, 1).Contains("cpu"), "Row rendered");
                            return Task.CompletedTask;
                        })
                });
        }

        private static void Type(TextEditor editor, string text)
        {
            foreach (char c in text)
                editor.HandleKey(KeyEvent.Char(c));
        }

        private static string ReadRow(CellBuffer buffer, int row)
        {
            StringBuilder builder = new StringBuilder();
            for (int x = 0; x < buffer.Width; x++)
                builder.Append(buffer.Get(x, row).Grapheme);

            return builder.ToString();
        }
    }
}

namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Hosting;
    using TUIKit.Input;
    using TUIKit.Terminal;
    using TUIKit.Widgets;

    /// <summary>
    /// Touchstone suite covering the focus manager (10.20), the form framework (10.15), async prompts
    /// (10.5), and find/replace (10.21).
    /// </summary>
    public static class FocusFormsPromptsSuite
    {
        private const string Esc = "\u001b";

        /// <summary>
        /// Builds the focus, forms, and prompts suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "FocusFormsPrompts",
                displayName: "Focus, Forms, Prompts, Find",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("FocusFormsPrompts", "FocusManager", "Focus cycles and routes keys",
                        _ =>
                        {
                            TextField a = new TextField();
                            TextField b = new TextField();
                            FocusManager focus = new FocusManager();
                            focus.Register(a, b);
                            Check.Equal(0, focus.FocusedIndex, "Starts on first");

                            focus.HandleKey(KeyEvent.Special(KeyCode.Tab));
                            Check.Equal(1, focus.FocusedIndex, "Tab moves to second");
                            focus.HandleKey(KeyEvent.Special(KeyCode.Tab));
                            Check.Equal(0, focus.FocusedIndex, "Tab wraps around");

                            focus.HandleKey(KeyEvent.Char('x')); // routed to focused field a
                            Check.Equal("x", a.Value, "Key routed to focused widget");
                            Check.Equal("", b.Value, "Unfocused widget untouched");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusFormsPrompts", "Form", "Form tabs between fields and validates",
                        _ =>
                        {
                            Form form = new Form();
                            TextField name = new TextField();
                            form.Add("Name", name, () => name.Value.Length == 0 ? "required" : null);
                            Checkbox verbose = form.Add("Verbose", new Checkbox("on"));
                            Check.Equal(2, form.FieldCount, "Two fields");
                            Check.Equal("required", form.Validate(), "Validation fails when empty");

                            form.HandleKey(KeyEvent.Char('a')); // to focused name field
                            Check.Equal("a", name.Value, "Typed into first field");
                            Check.True(form.Validate() == null, "Valid once filled");

                            form.HandleKey(KeyEvent.Special(KeyCode.Tab));
                            Check.Equal(1, form.FocusedIndex, "Tab moved focus");
                            form.HandleKey(KeyEvent.Char(' ')); // toggle checkbox
                            Check.True(verbose.Checked, "Checkbox toggled via form focus");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusFormsPrompts", "ConfirmAsync", "Confirm dialog returns the choice",
                        async ct =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 10);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                Task<bool> confirm = app.ConfirmAsync("Proceed?", "Yes", "No");
                                backend.FeedInput(new byte[] { 0x0D }); // Enter chooses the first button
                                app.PumpInputOnce();
                                bool result = await confirm.ConfigureAwait(false);
                                Check.True(result, "Enter chose Yes");
                            }
                        }),

                    new TestCaseDescriptor("FocusFormsPrompts", "PromptAsync", "Prompt returns typed text and cancels on escape",
                        async ct =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 10);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                Task<string?> prompt = app.PromptAsync("Name?");
                                backend.FeedInput("hi");
                                backend.FeedInput(new byte[] { 0x0D });
                                app.PumpInputOnce();
                                Check.Equal("hi", await prompt.ConfigureAwait(false), "Returned typed text");

                                Task<string?> cancelled = app.PromptAsync("Name?");
                                backend.FeedInput(new byte[] { 0x1B }); // Escape
                                app.PumpInputOnce();
                                app.PumpInputOnce(); // flush the lone escape
                                Check.True(await cancelled.ConfigureAwait(false) == null, "Escape cancels to null");
                            }
                        }),

                    new TestCaseDescriptor("FocusFormsPrompts", "SelectAsync", "Select returns the chosen index",
                        async ct =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 10);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                Task<int> select = app.SelectAsync("Pick", "a", "b", "c");
                                backend.FeedInput(Esc + "[B"); // Down arrow -> index 1
                                backend.FeedInput(new byte[] { 0x0D });
                                app.PumpInputOnce();
                                Check.Equal(1, await select.ConfigureAwait(false), "Chose the second option");
                            }
                        }),

                    new TestCaseDescriptor("FocusFormsPrompts", "PaneFind", "Pane search finds and highlights matches",
                        _ =>
                        {
                            Pane pane = new Pane("p");
                            pane.WriteLine("apple");
                            pane.WriteLine("banana");
                            pane.WriteLine("cherry");

                            CellBuffer buffer = new CellBuffer(20, 2);
                            BufferSurface surface = new BufferSurface(buffer);
                            pane.Render(surface, CellStyle.Default);

                            Check.False(pane.FindNext(), "No search set");
                            pane.SetSearch("an");
                            Check.True(pane.FindNext(), "Found 'an' in banana");

                            pane.SetSearch("banana");
                            pane.ScrollToBottom();
                            buffer = new CellBuffer(20, 2);
                            pane.Render(new BufferSurface(buffer), CellStyle.Default);
                            bool highlighted = false;
                            for (int y = 0; y < 2 && !highlighted; y++)
                            {
                                for (int x = 0; x < 6; x++)
                                {
                                    if (buffer.Get(x, y).Style.Background == Color.FromPalette(3))
                                    {
                                        highlighted = true;
                                        break;
                                    }
                                }
                            }

                            Check.True(highlighted, "Match highlighted with the search style");

                            pane.SetSearch(null);
                            Check.False(pane.FindNext(), "Cleared search finds nothing");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusFormsPrompts", "EditorFindReplace", "Editor find and replace-all",
                        _ =>
                        {
                            TextEditor editor = new TextEditor();
                            editor.Text = "foo bar foo";
                            editor.HandleKey(KeyEvent.Special(KeyCode.Home));

                            Check.Equal(2, editor.ReplaceAll("foo", "X"), "Replaced two occurrences");
                            Check.Equal("X bar X", editor.Text, "Text after replace");

                            Check.True(editor.Find("bar"), "Found 'bar'");
                            Check.False(editor.Find("zzz"), "Missing term not found");
                            Check.Equal(0, editor.ReplaceAll("nope", "y"), "No replacements for missing term");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}

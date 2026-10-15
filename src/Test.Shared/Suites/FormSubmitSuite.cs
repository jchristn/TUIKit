namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Testing;
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for 1.9 item C6 submit and cancel: <see cref="Form.Submit"/>, <see cref="Form.Cancel"/>,
    /// <see cref="Form.SubmitRequested"/>, <see cref="Form.CancelRequested"/>, <see cref="Form.SubmitKey"/>,
    /// <see cref="Form.CancelKey"/>, <see cref="Form.ShowButtons"/>, the labels, and <see cref="Form.Buttons"/>.
    /// </summary>
    public static class FormSubmitSuite
    {
        private const string _Id = "FormSubmit";

        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: _Id,
                displayName: "Form Submit and Cancel",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(_Id, "SubmitErrors", "Submit with errors focuses the first invalid field and raises nothing; without errors it raises once",
                        _ =>
                        {
                            Form form = FormValidationSuite.Build(out TextField name, out TextField email, out TextField port);
                            name.Value = "ada";
                            int submitted = 0;
                            form.SubmitRequested += (s, e) => submitted++;
                            form.SetFocusedField(2);
                            Check.False(form.Submit(), "invalid");
                            Check.Equal(1, form.FocusedIndex, "first invalid field (Email) focused");
                            Check.Equal(0, submitted, "not raised");
                            email.Value = "a@b";
                            port.Value = "80";
                            Check.True(form.Submit(), "valid");
                            Check.Equal(1, submitted, "raised once");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Cancel", "Cancel raises CancelRequested and leaves the decision to the handler",
                        _ =>
                        {
                            Form form = FormLayoutSuite.Three();
                            bool dirtyWhenCancelled = true;
                            form.CancelRequested += (s, e) => dirtyWhenCancelled = form.IsDirty;
                            form.Cancel();
                            Check.False(dirtyWhenCancelled, "handler read IsDirty");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Keys", "Submit and cancel keys are unbound by default and consumed once set",
                        _ =>
                        {
                            Form form = FormLayoutSuite.Three();
                            int submits = 0;
                            int cancels = 0;
                            form.SubmitRequested += (s, e) => submits++;
                            form.CancelRequested += (s, e) => cancels++;
                            Check.False(form.HandleKey(KeyEvent.Char('s', KeyModifiers.Ctrl)), "Ctrl+S not consumed");
                            Check.False(form.HandleKey(KeyEvent.Special(KeyCode.Escape)), "Esc not consumed");
                            form.SubmitKey = KeyChord.Parse("ctrl+s");
                            form.CancelKey = KeyChord.Parse("escape");
                            Check.True(form.HandleKey(KeyEvent.Char('s', KeyModifiers.Ctrl)), "Ctrl+S consumed");
                            Check.True(form.HandleKey(KeyEvent.Special(KeyCode.Escape)), "Esc consumed");
                            Check.Equal(1, submits, "submitted");
                            Check.Equal(1, cancels, "cancelled");
                            Check.Equal("Save,Cancel", string.Join(",", form.GetKeyHints()!.Select(h => h.Description)), "hints");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Buttons", "Buttons appear only with ShowButtons, join the focus order, and call Submit and Cancel",
                        _ =>
                        {
                            Form form = FormLayoutSuite.Three();
                            Check.False(Snapshot.RenderWidget(form, 30, 12).Contains("Save"), "no buttons by default");
                            form.ShowButtons = true;
                            form.SubmitLabel = "Create";
                            string screen = Snapshot.RenderWidget(form, 30, 12);
                            Check.True(screen.Contains("Create") && screen.Contains("Cancel"), "buttons drawn: " + screen);
                            Check.True(form.Buttons != null && form.Buttons.Buttons.Count == 2, "exposed");
                            int submits = 0;
                            form.SubmitRequested += (s, e) => submits++;
                            for (int i = 0; i < 3; i++)
                                form.HandleKey(KeyEvent.Special(KeyCode.Tab));
                            Check.Equal(-1, form.FocusedIndex, "the buttons hold focus");
                            Check.True(form.FocusedLeaf is Button, "a button is the leaf");
                            form.HandleKey(KeyEvent.Special(KeyCode.Enter));
                            Check.Equal(1, submits, "Create submitted");
                            form.HandleKey(KeyEvent.Special(KeyCode.Tab));
                            form.HandleKey(KeyEvent.Special(KeyCode.Tab));
                            Check.Equal(0, form.FocusedIndex, "Tab wraps back to the first field");
                            form.Add("Late", new TextField());
                            for (int i = 0; i < 4; i++)
                                form.HandleKey(KeyEvent.Special(KeyCode.Tab));
                            Check.Equal(-1, form.FocusedIndex, "a field added later still comes before the buttons");
                            form.ShowButtons = false;
                            Check.False(Snapshot.RenderWidget(form, 30, 14).Contains("Create"), "hidden again");
                            Check.Throws<ArgumentNullException>(() => form.SubmitLabel = null!, "null label");
                            Check.Throws<ArgumentNullException>(() => form.CancelLabel = null!, "null cancel label");
                            return Task.CompletedTask;
                        }),
                    new TestCaseDescriptor(_Id, "MouseAndTheme", "A click on Save submits; the theme reaches sections, hints, errors, and the buttons; the focus rect covers the buttons",
                        _ =>
                        {
                            Form form = FormLayoutSuite.Three();
                            form.ShowButtons = true;
                            int submits = 0;
                            form.SubmitRequested += (s, e) => submits++;
                            string[] lines = Snapshot.RenderWidget(form, 30, 12).Split('\n');
                            int row = Array.FindIndex(lines, l => l.Contains("Save"));
                            int x = lines[row].IndexOf("Save", StringComparison.Ordinal);
                            form.HandleMouse(new MouseEvent(MouseEventKind.Press, MouseButton.Left, x + 1, row, KeyModifiers.None, 1));
                            form.HandleMouse(new MouseEvent(MouseEventKind.Release, MouseButton.Left, x + 1, row, KeyModifiers.None, 1));
                            Check.Equal(1, submits, "clicked Save");
                            Check.True(form.TryGetFocusRect(out Rect rect) && rect.Y == row, "focus rect on the buttons");
                            TUIKit.Theming.Theme theme = TUIKit.Theming.Theme.Light;
                            form.ApplyTheme(theme);
                            Check.Equal(theme.Error, form.ErrorStyle, "error style from the theme");
                            Check.Equal(theme.Muted, form.HintStyle, "hint style");
                            Check.Equal(theme.Accent.WithAttribute(CellAttributes.Bold, true), form.SectionStyle, "section style");
                            form.Clear();
                            Check.Equal(0, form.FieldCount, "cleared");
                            Check.True(form.Buttons != null, "buttons kept");
                            TextField first = form.Add("First", new TextField());
                            Check.True(ReferenceEquals(form.FocusedLeaf, first), "the first field after a rebuild takes focus");
                            return Task.CompletedTask;
                        }),
                });
        }
    }
}

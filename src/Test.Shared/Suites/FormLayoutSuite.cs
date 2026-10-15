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
    /// Coverage for 1.9 item C6 layout: the 1.7 layout with defaults, <see cref="Form.AddSection"/>,
    /// <see cref="Form.SetFieldHint"/>, <see cref="Form.LabelPlacement"/>, <see cref="Form.LabelWidth"/>, and
    /// <see cref="Form.ArrowKeysMoveFocus"/>.
    /// </summary>
    public static class FormLayoutSuite
    {
        private const string _Id = "FormLayout";

        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: _Id,
                displayName: "Form Layout",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(_Id, "DefaultLayout", "With defaults a three-field form renders the 1.7 layout",
                        _ =>
                        {
                            Form form = Three();
                            string expected = "> Name\n  ada\n\n  Email\n  a@b.c\n\n  [x] Admin";
                            Check.Equal(expected, Snapshot.RenderWidget(form, 30, 8).TrimEnd(), "1.7 layout");
                            Check.Equal(8, form.ContentHeight, "content height");
                            Check.True(form.GetKeyHints() == null, "no hints by default");
                            Check.True(form.Buttons == null, "no buttons by default");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Sections", "Sections draw before their field with a blank row, take no focus, and do not shift indexes",
                        _ =>
                        {
                            Form form = new Form();
                            form.AddSection("Account");
                            form.Add("Name", new TextField { Value = "ada" });
                            form.AddSection("Contact");
                            TextField email = form.Add("Email", new TextField());
                            form.AddSection("End");
                            string[] lines = Snapshot.RenderWidget(form, 30, 12).Split('\n');
                            Check.Equal("Account", lines[0], "first section at the top, no blank row");
                            Check.Equal("> Name", lines[1], "field after the section");
                            Check.Equal(string.Empty, lines[4], "blank row before a later section");
                            Check.Equal("Contact", lines[5], "second section");
                            Check.True(lines.Contains("End"), "trailing section after the last field");
                            Check.Equal(2, form.FieldCount, "sections are not fields");
                            Check.True(ReferenceEquals(email, form.GetField(1)), "indexes not shifted");
                            form.HandleKey(KeyEvent.Special(KeyCode.Tab));
                            Check.Equal(1, form.FocusedIndex, "Tab skips the section");
                            Check.Throws<ArgumentNullException>(() => form.AddSection(null!), "null title");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Hints", "A hint wraps under its field in HintStyle; null clears it",
                        _ =>
                        {
                            Form form = new Form();
                            form.Add("Prefix", new TextField());
                            form.SetFieldHint(0, "Used as the branch prefix for new work");
                            CellBuffer buffer = MarkdownThemeSuite.Draw(form, 22, 8);
                            string[] lines = Snapshot.ToText(buffer).Split('\n');
                            Check.Equal("  Used as the branch", lines[2], "wrapped first line");
                            Check.Equal("  prefix for new work", lines[3], "wrapped second line");
                            Check.True((buffer.Get(2, 2).Style.Attributes & CellAttributes.Dim) != 0, "dim hint");
                            form.SetFieldHint(0, null);
                            Check.Equal(string.Empty, Snapshot.RenderWidget(form, 22, 8).Split('\n')[2], "cleared");
                            Check.Throws<ArgumentOutOfRangeException>(() => form.SetFieldHint(5, "x"), "bad index");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "LeftLabels", "Left placement aligns labels, keeps checkboxes inline, and starts a multi-row field on the label row",
                        _ =>
                        {
                            Form form = new Form { LabelPlacement = FormLabelPlacement.Left };
                            form.Add("Name", new TextField { Value = "ada" });
                            form.Add("Bio", new TextEditor { Text = "line one\nline two" });
                            form.AddCheckbox("Admin", true);
                            string[] lines = Snapshot.RenderWidget(form, 40, 10).Split('\n');
                            Check.Equal("> Name ada", lines[0], "label beside the field (width = Name + 3)");
                            Check.Equal("  Bio  line one", lines[2], "multi-row field starts on the label row");
                            Check.True(lines.Any(l => l == "  [x] Admin"), "checkbox inline");
                            form.LabelWidth = 10;
                            Check.Equal("> Name    ada", Snapshot.RenderWidget(form, 40, 10).Split('\n')[0], "explicit label width");
                            Check.Throws<ArgumentOutOfRangeException>(() => form.LabelWidth = -1, "negative width");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "ArrowKeys", "ArrowKeysMoveFocus false leaves Up and Down to the field; true moves between TextFields but not out of a TextEditor",
                        _ =>
                        {
                            Form form = new Form();
                            form.Add("A", new TextField());
                            form.Add("B", new TextEditor { Text = "x\ny" });
                            form.Add("C", new TextField());
                            Check.False(form.HandleKey(KeyEvent.Special(KeyCode.Down)), "off: Down not consumed");
                            Check.Equal(0, form.FocusedIndex, "focus unchanged");
                            form.ArrowKeysMoveFocus = true;
                            Check.True(form.HandleKey(KeyEvent.Special(KeyCode.Down)), "Down moves");
                            Check.Equal(1, form.FocusedIndex, "into the editor");
                            form.HandleKey(KeyEvent.Special(KeyCode.Down));
                            Check.Equal(1, form.FocusedIndex, "the editor consumed Down");
                            Check.True(form.GetKeyHints()!.Any(h => h.Description == "Field"), "hint");
                            form.SetFocusedField(2);
                            form.HandleKey(KeyEvent.Special(KeyCode.Up));
                            Check.Equal(1, form.FocusedIndex, "Up from C into the editor");
                            return Task.CompletedTask;
                        }),
                });
        }

        internal static Form Three()
        {
            Form form = new Form();
            form.Add("Name", new TextField { Value = "ada" });
            form.Add("Email", new TextField { Value = "a@b.c" });
            form.AddCheckbox("Admin", true);
            return form;
        }
    }
}

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
    /// Coverage for 1.8 item C1: <see cref="TextField.SelectAll"/>, <see cref="TextField.ClearSelection"/>,
    /// <see cref="TextField.IsAllSelected"/>, <see cref="TextField.SelectionStyle"/>, and <see cref="TextField.MaxLength"/>.
    /// </summary>
    public static class TextFieldPrefillSuite
    {
        private const string _Id = "TextFieldPrefill";

        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: _Id,
                displayName: "TextField Prefill and Length",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(_Id, "TypeReplaces", "After SelectAll the first printable key replaces the value",
                        _ =>
                        {
                            TextField field = new TextField { Value = "remembered" };
                            Check.False(field.IsAllSelected, "nothing selected by default");
                            field.SelectAll();
                            Check.True(field.IsAllSelected, "selected");
                            field.HandleKey(KeyEvent.Char('x'));
                            Check.Equal("x", field.Value, "replaced");
                            Check.False(field.IsAllSelected, "selection dropped");
                            field.HandleKey(KeyEvent.Char('y'));
                            Check.Equal("xy", field.Value, "later keys append");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "PasteReplaces", "A paste replaces a selected value",
                        _ =>
                        {
                            TextField field = new TextField { Value = "old" };
                            field.SelectAll();
                            field.Insert("new");
                            Check.Equal("new", field.Value, "replaced by paste");
                            Check.True(field.HandlePaste("!"), "HandlePaste consumes");
                            Check.Equal("new!", field.Value, "appended without a selection");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "DeleteClears", "Backspace and Delete clear a selected value",
                        _ =>
                        {
                            foreach (KeyCode code in new[] { KeyCode.Backspace, KeyCode.Delete })
                            {
                                TextField field = new TextField { Value = "abc" };
                                field.SelectAll();
                                Check.True(field.HandleKey(KeyEvent.Special(code)), code + " consumed");
                                Check.Equal(string.Empty, field.Value, code + " cleared");
                            }

                            TextField lineField = new TextField { Value = "abc", UseLineEditingKeys = true };
                            lineField.SelectAll();
                            lineField.HandleKey(TextFieldEditingSuite.Ctrl('u'));
                            Check.Equal(string.Empty, lineField.Value, "Ctrl+U cleared");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "MovementKeeps", "Caret movement keeps the value and drops the selection; setting Value clears it",
                        _ =>
                        {
                            foreach (KeyCode code in new[] { KeyCode.Left, KeyCode.Right, KeyCode.Home, KeyCode.End })
                            {
                                TextField field = new TextField { Value = "keep" };
                                field.SelectAll();
                                field.HandleKey(KeyEvent.Special(code));
                                Check.Equal("keep", field.Value, code + " kept the value");
                                Check.False(field.IsAllSelected, code + " dropped the selection");
                            }

                            TextField set = new TextField { Value = "a" };
                            set.SelectAll();
                            set.Value = "b";
                            Check.False(set.IsAllSelected, "Value set clears");
                            set.SelectAll();
                            set.ClearSelection();
                            Check.False(set.IsAllSelected, "ClearSelection");
                            Check.Equal("b", set.Value, "ClearSelection keeps the value");
                            TextField empty = new TextField();
                            empty.SelectAll();
                            Check.False(empty.IsAllSelected, "an empty value cannot be selected");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "SelectionStyle", "The selected value renders in SelectionStyle, reverse video by default",
                        _ =>
                        {
                            TextField field = new TextField { Value = "sel" };
                            CellBuffer plain = MarkdownThemeSuite.Draw(field, 6, 1);
                            Check.True((plain.Get(0, 0).Style.Attributes & CellAttributes.Reverse) == 0, "unselected draws normally");
                            field.SelectAll();
                            CellBuffer selected = MarkdownThemeSuite.Draw(field, 6, 1);
                            Check.True((selected.Get(0, 0).Style.Attributes & CellAttributes.Reverse) != 0, "selected draws reversed");
                            CellStyle custom = CellStyle.Default.WithForeground(Color.FromPalette(3));
                            field.SelectionStyle = custom;
                            Check.Equal(custom, MarkdownThemeSuite.Draw(field, 6, 1).Get(1, 0).Style, "custom style used");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "MaxLength", "MaxLength refuses typing at the limit, truncates a paste per grapheme, and never truncates Value",
                        _ =>
                        {
                            TextField field = new TextField();
                            Check.Equal(0, field.MaxLength, "no limit by default");
                            Check.Throws<ArgumentOutOfRangeException>(() => field.MaxLength = -1, "negative rejected");
                            field.MaxLength = 3;
                            foreach (char c in "abcd")
                                Check.True(field.HandleKey(KeyEvent.Char(c)), "typing consumed: " + c);
                            Check.Equal("abc", field.Value, "fourth key refused");
                            field.Value = string.Empty;
                            field.Insert("x\U0001F600yz");
                            Check.Equal("x\U0001F600y", field.Value, "paste keeps the leading graphemes, emoji whole");
                            field.Value = "stored value longer";
                            Check.Equal("stored value longer", field.Value, "programmatic set not truncated");
                            field.HandleKey(KeyEvent.Char('q'));
                            Check.Equal("stored value longer", field.Value, "but typing past the limit is refused");
                            field.SelectAll();
                            field.HandleKey(KeyEvent.Char('q'));
                            Check.Equal("q", field.Value, "replacing a selection works at the limit");
                            return Task.CompletedTask;
                        }),
                });
        }
    }
}

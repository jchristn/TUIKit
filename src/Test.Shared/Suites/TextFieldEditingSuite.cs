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
    /// Coverage for 1.8 item C1: <see cref="TextField.UseLineEditingKeys"/>, <see cref="TextField.RevealKey"/>,
    /// <see cref="TextField.Submitted"/>, <see cref="TextField.ConsumesChord"/>, key hints, and the per-grapheme mask.
    /// </summary>
    public static class TextFieldEditingSuite
    {
        private const string _Id = "TextFieldEditing";

        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: _Id,
                displayName: "TextField Editing Keys",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(_Id, "OffByDefault", "With UseLineEditingKeys false, each line-editing chord is refused and changes nothing",
                        _ =>
                        {
                            TextField field = Field("hello world", 5);
                            foreach (char c in "aeukw")
                            {
                                Check.False(field.HandleKey(Ctrl(c)), "Ctrl+" + c + " not consumed");
                                Check.Equal("hello world", field.Value, "value after Ctrl+" + c);
                                Check.Equal(5, field.CaretIndex, "caret after Ctrl+" + c);
                            }

                            Check.True(field.HandleKey(KeyEvent.Special(KeyCode.Left, KeyModifiers.Ctrl)), "Ctrl+Left still moves (1.7)");
                            Check.Equal(4, field.CaretIndex, "one grapheme, not a word");
                            Check.False(field.UseLineEditingKeys, "default false");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "MoveChords", "Ctrl+A, Ctrl+E, Ctrl+Left, and Ctrl+Right move by line and by word",
                        _ =>
                        {
                            TextField field = Field("one two  three", 6);
                            field.UseLineEditingKeys = true;
                            Check.True(field.HandleKey(Ctrl('a')), "Ctrl+A");
                            Check.Equal(0, field.CaretIndex, "start");
                            Check.True(field.HandleKey(Ctrl('e')), "Ctrl+E");
                            Check.Equal(14, field.CaretIndex, "end");
                            field.HandleKey(KeyEvent.Special(KeyCode.Left, KeyModifiers.Ctrl));
                            Check.Equal(9, field.CaretIndex, "start of 'three'");
                            field.HandleKey(KeyEvent.Special(KeyCode.Left, KeyModifiers.Ctrl));
                            Check.Equal(4, field.CaretIndex, "start of 'two' across two spaces");
                            field.HandleKey(KeyEvent.Special(KeyCode.Right, KeyModifiers.Ctrl));
                            Check.Equal(9, field.CaretIndex, "next word start");
                            field.HandleKey(KeyEvent.Special(KeyCode.Right, KeyModifiers.Ctrl));
                            Check.Equal(14, field.CaretIndex, "end when no next word");
                            field.HandleKey(KeyEvent.Special(KeyCode.Right, KeyModifiers.Ctrl));
                            Check.Equal(14, field.CaretIndex, "stays at end");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "DeleteChords", "Ctrl+U, Ctrl+K, and Ctrl+W delete at the start, middle, and end",
                        _ =>
                        {
                            TextField field = Field("alpha beta gamma", 10);
                            field.UseLineEditingKeys = true;
                            int changes = 0;
                            field.Changed += (s, e) => changes++;
                            Check.True(field.HandleKey(Ctrl('k')), "Ctrl+K");
                            Check.Equal("alpha beta", field.Value, "deleted to end");
                            Check.Equal(1, changes, "one Changed per edit");
                            Check.True(field.HandleKey(Ctrl('w')), "Ctrl+W");
                            Check.Equal("alpha ", field.Value, "deleted last word");
                            Check.True(field.HandleKey(Ctrl('w')), "Ctrl+W over trailing space");
                            Check.Equal(string.Empty, field.Value, "trailing space then word");
                            field.Value = "abc def";
                            field.HandleKey(KeyEvent.Special(KeyCode.Left));
                            field.HandleKey(KeyEvent.Special(KeyCode.Left));
                            Check.True(field.HandleKey(Ctrl('u')), "Ctrl+U");
                            Check.Equal("ef", field.Value, "deleted to start");
                            Check.Equal(0, field.CaretIndex, "caret at start");
                            changes = 0;
                            Check.True(field.HandleKey(Ctrl('u')), "Ctrl+U at start is consumed");
                            Check.Equal(0, changes, "no change raised at start");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Graphemes", "Word edits over CJK and emoji keep whole graphemes",
                        _ =>
                        {
                            string emoji = "\U0001F600";
                            TextField field = Field("日本 " + emoji + emoji, -1);
                            field.UseLineEditingKeys = true;
                            field.HandleKey(KeyEvent.Special(KeyCode.Left, KeyModifiers.Ctrl));
                            Check.Equal(3, field.CaretIndex, "word start lands before the emoji pair");
                            field.HandleKey(Ctrl('e'));
                            field.HandleKey(Ctrl('w'));
                            Check.Equal("日本 ", field.Value, "emoji word removed whole");
                            field.HandleKey(Ctrl('w'));
                            Check.Equal(string.Empty, field.Value, "CJK word removed whole");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "ReadOnly", "A read-only field refuses deleting chords but still moves",
                        _ =>
                        {
                            TextField field = Field("read only", 4);
                            field.UseLineEditingKeys = true;
                            field.IsReadOnly = true;
                            Check.False(field.HandleKey(Ctrl('u')), "Ctrl+U refused");
                            Check.False(field.HandleKey(Ctrl('k')), "Ctrl+K refused");
                            Check.False(field.HandleKey(Ctrl('w')), "Ctrl+W refused");
                            Check.Equal("read only", field.Value, "unchanged");
                            Check.True(field.HandleKey(Ctrl('e')), "Ctrl+E moves");
                            Check.Equal(9, field.CaretIndex, "moved to end");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "ConsumesChordMatches", "ConsumesChord agrees with HandleKey for every chord in both flag states",
                        _ =>
                        {
                            KeyEvent[] keys =
                            {
                                Ctrl('a'), Ctrl('e'), Ctrl('u'), Ctrl('k'), Ctrl('w'), Ctrl('r'), Ctrl('z'),
                                KeyEvent.Special(KeyCode.Left, KeyModifiers.Ctrl), KeyEvent.Special(KeyCode.Right, KeyModifiers.Ctrl),
                                KeyEvent.Special(KeyCode.Enter), KeyEvent.Special(KeyCode.Enter, KeyModifiers.Ctrl),
                                KeyEvent.Special(KeyCode.Backspace), KeyEvent.Special(KeyCode.Tab), KeyEvent.Special(KeyCode.Up),
                            };
                            foreach (bool lineKeys in new[] { false, true })
                            {
                                foreach (bool readOnly in new[] { false, true })
                                {
                                    foreach (KeyEvent key in keys)
                                    {
                                        TextField field = Field("abc def", 4);
                                        field.UseLineEditingKeys = lineKeys;
                                        field.IsReadOnly = readOnly;
                                        bool predicted = field.ConsumesChord(KeyChord.FromKeyEvent(key));
                                        bool actual = field.HandleKey(key);
                                        Check.Equal(actual, predicted, key + " lineKeys=" + lineKeys + " readOnly=" + readOnly);
                                    }
                                }
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "RevealKey", "RevealKey is unbound by default, toggles a masked field, and is not consumed on an unmasked one",
                        _ =>
                        {
                            TextField field = Field("pw", -1);
                            field.MaskChar = '*';
                            Check.True(field.RevealKey == null, "default null");
                            Check.False(field.HandleKey(Ctrl('r')), "Ctrl+R not consumed without a key");
                            field.RevealKey = KeyChord.Parse("ctrl+r");
                            Check.True(field.ConsumesChord(KeyChord.Parse("ctrl+r")), "reported");
                            Check.True(field.HandleKey(Ctrl('r')), "consumed");
                            Check.True(field.IsRevealed, "revealed");
                            Check.True(field.IsMasked, "still reports the mask");
                            Check.Equal("pw", Snapshot.RenderWidget(field, 6, 1).TrimEnd(), "plain value drawn");
                            field.HandleKey(Ctrl('r'));
                            Check.False(field.IsRevealed, "hidden again");
                            Check.Equal("**", Snapshot.RenderWidget(field, 6, 1).TrimEnd(), "masked again");
                            TextField plain = Field("x", -1);
                            plain.RevealKey = KeyChord.Parse("ctrl+r");
                            Check.False(plain.HandleKey(Ctrl('r')), "unmasked field leaves the key");
                            Check.False(plain.ConsumesChord(KeyChord.Parse("ctrl+r")), "and does not report it");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Submitted", "Enter is consumed only with a Submitted handler; modified Enter never is",
                        _ =>
                        {
                            TextField field = Field("x", -1);
                            Check.False(field.HandleKey(KeyEvent.Special(KeyCode.Enter)), "no handler leaves Enter");
                            Check.False(field.ConsumesChord(KeyChord.Parse("enter")), "not reported without a handler");
                            int count = 0;
                            field.Submitted += (s, e) => count++;
                            Check.True(field.ConsumesChord(KeyChord.Parse("enter")), "reported with a handler");
                            Check.True(field.HandleKey(KeyEvent.Special(KeyCode.Enter)), "consumed with a handler");
                            Check.Equal(1, count, "raised once");
                            Check.False(field.HandleKey(KeyEvent.Special(KeyCode.Enter, KeyModifiers.Ctrl)), "Ctrl+Enter not consumed");
                            Check.Equal(1, count, "not raised for Ctrl+Enter");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "KeyHints", "Hints are null by default and list Show/Hide and Submit only when in use",
                        _ =>
                        {
                            TextField field = new TextField();
                            Check.True(field.GetKeyHints() == null, "null with defaults");
                            field.RevealKey = KeyChord.Parse("ctrl+r");
                            Check.True(field.GetKeyHints() == null, "reveal key on an unmasked field adds nothing");
                            field.MaskChar = '*';
                            IReadOnlyList<KeyHint>? hints = field.GetKeyHints();
                            Check.True(hints != null && hints.Count == 1 && hints[0].Description == "Show", "Show hint");
                            field.IsRevealed = true;
                            Check.Equal("Hide", field.GetKeyHints()![0].Description, "Hide while revealed");
                            field.Submitted += (s, e) => { };
                            hints = field.GetKeyHints();
                            Check.True(hints != null && hints.Count == 2 && hints[1].Description == "Submit", "Submit hint");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "MaskPerGrapheme", "A masked emoji draws one mask cell and the caret sits under the right cell",
                        _ =>
                        {
                            TextField field = new TextField { MaskChar = '*', IsFocused = true };
                            field.Value = "a\U0001F600b";
                            CellBuffer buffer = MarkdownThemeSuite.Draw(field, 8, 1);
                            Check.Equal("***", Snapshot.ToText(buffer).TrimEnd(), "three mask cells for three graphemes");
                            Check.True((buffer.Get(3, 0).Style.Attributes & CellAttributes.Reverse) != 0, "caret after the value");
                            field.HandleKey(KeyEvent.Special(KeyCode.Left));
                            buffer = MarkdownThemeSuite.Draw(field, 8, 1);
                            Check.True((buffer.Get(2, 0).Style.Attributes & CellAttributes.Reverse) != 0, "caret under the third cell");
                            field.HandleKey(KeyEvent.Special(KeyCode.Left));
                            buffer = MarkdownThemeSuite.Draw(field, 8, 1);
                            Check.True((buffer.Get(1, 0).Style.Attributes & CellAttributes.Reverse) != 0, "caret under the emoji's cell");
                            Check.Equal("*", buffer.Get(1, 0).Grapheme, "the caret shows the mask");
                            Check.True(field.HandleMouse(new MouseEvent(MouseEventKind.Press, MouseButton.Left, 2, 0, KeyModifiers.None, 1)), "click");
                            Check.Equal(3, field.CaretIndex, "click on the third cell maps past the surrogate pair");
                            return Task.CompletedTask;
                        }),
                });
        }

        internal static KeyEvent Ctrl(char c)
        {
            return KeyEvent.Char(c, KeyModifiers.Ctrl);
        }

        internal static TextField Field(string value, int caret)
        {
            TextField field = new TextField();
            field.Value = value;
            if (caret >= 0)
            {
                field.HandleKey(KeyEvent.Special(KeyCode.Home));
                while (field.CaretIndex < caret)
                    field.HandleKey(KeyEvent.Special(KeyCode.Right));
            }

            return field;
        }
    }
}

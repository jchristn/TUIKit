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
    /// Coverage for key hints over real text fields (1.5.0, B6): <see cref="ITextEntryKeys"/>,
    /// <see cref="KeyHint.WhileTyping"/>, <see cref="KeyHint.WithTypingAlternative"/>,
    /// <see cref="IKeyHintSourceOptions"/>, <see cref="StatusBar.RightText"/>, and <see cref="StatusBar.ReservedHint"/>.
    /// </summary>
    public static class TypingHintsSuite
    {
        /// <summary>
        /// Builds the typing hints suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "TypingHints",
                displayName: "Key Hints for Real Text Fields",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("TypingHints", "EditorHidesConsumedKeys", "In a TextEditor, Enter and the arrows are hidden, Esc stays, and Ctrl+S follows ConsumeUnboundControlKeys",
                        _ =>
                        {
                            TextEditor editor = new TextEditor { ConsumeUnboundControlKeys = false };
                            HintContainer container = new HintContainer(editor)
                            {
                                Hints = new[] { KeyHint.For("enter", "Send"), KeyHint.For("up", "History"), KeyHint.For("esc", "Back"), KeyHint.For("ctrl+s", "Save"), KeyHint.For("tab", "Next") }
                            };
                            List<string> keys = Keys(new KeyHintResolver { ShowLeaveTextHint = false }.Resolve(FocusPath.Build("main", container)));
                            Check.False(keys.Contains("Enter"), "Enter is consumed by the editor");
                            Check.False(keys.Contains("Up"), "Up is consumed");
                            Check.True(keys.Contains("Esc"), "Esc reaches the app, so it stays");
                            Check.True(keys.Contains("Tab"), "Tab is not consumed by the editor");
                            Check.True(keys.Contains("Ctrl+S"), "Ctrl+S reaches the app while unbound control keys fall through");

                            editor.ConsumeUnboundControlKeys = true;
                            keys = Keys(new KeyHintResolver { ShowLeaveTextHint = false }.Resolve(FocusPath.Build("main", container)));
                            Check.False(keys.Contains("Ctrl+S"), "Ctrl+S hidden when the editor swallows it");

                            TextField field = new TextField();
                            HintContainer form = new HintContainer(field) { Hints = new[] { KeyHint.For("enter", "Submit"), KeyHint.For("left", "Back") } };
                            keys = Keys(new KeyHintResolver().Resolve(FocusPath.Build("form", form)));
                            Check.True(keys.Contains("Enter"), "a TextField does not consume Enter");
                            Check.False(keys.Contains("Left"), "it does consume Left");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("TypingHints", "WhileTypingAndAlternative", "A / hint marked WhileTyping stays while typing, and ? with a typing alternative shows F1",
                        _ =>
                        {
                            KeyHint slash = new KeyHint("/", "Command").WhileTyping();
                            KeyHint help = new KeyHint("?", "Help").WithTypingAlternative(KeyHint.For("f1", "Help"));
                            TextField field = new TextField();
                            HintContainer container = new HintContainer(field) { Hints = new[] { slash, help, new KeyHint("q", "Quit") } };
                            List<string> typing = Keys(new KeyHintResolver().Resolve(FocusPath.Build("main", container)));
                            Check.True(typing.Contains("/"), "the slash hint stays");
                            Check.True(typing.Contains("F1"), "help shows F1 while typing");
                            Check.False(typing.Contains("?"), "not ?");
                            Check.False(typing.Contains("Q"), "an unmarked letter is still hidden");

                            HintContainer idle = new HintContainer(new ListView<string>()) { Hints = new[] { slash, help } };
                            List<string> notTyping = Keys(new KeyHintResolver().Resolve(FocusPath.Build("main", idle)));
                            Check.True(notTyping.Contains("?"), "? when not typing");
                            Check.False(notTyping.Contains("F1"), "no F1 when not typing");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("TypingHints", "PerFieldLeaveHint", "A field's LeaveHint wins over the resolver's",
                        _ =>
                        {
                            TextField search = new TextField { LeaveHint = new KeyHint("Esc", "Back to list") };
                            IReadOnlyList<KeyHint> hints = new KeyHintResolver().Resolve(FocusPath.Build("search", search));
                            Check.Equal("Esc", hints[0].Key, "the field's own leave hint comes first");
                            Check.False(hints.Any(h => h.Key == "Tab" && h.Description == "Next field"), "the global leave hint is replaced");

                            TextField plain = new TextField();
                            Check.Equal("Tab", new KeyHintResolver().Resolve(FocusPath.Build("f", plain))[0].Key, "without one, the resolver's hint is used");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("TypingHints", "OwnFirstAndExclusive", "OwnFirst puts a container's hints ahead of its child's, and Exclusive drops outer sources but keeps app hints",
                        _ =>
                        {
                            HintLeaf leaf = new HintLeaf { Hints = new[] { new KeyHint("F2", "Rename") } };
                            OptionsHintContainer inner = new OptionsHintContainer(leaf) { Hints = new[] { new KeyHint("F3", "Pin") } };
                            OptionsHintContainer outer = new OptionsHintContainer(inner) { Hints = new[] { new KeyHint("F4", "Outer") } };
                            KeyHintResolver resolver = new KeyHintResolver();
                            resolver.AddAppHint("ctrl+q", "Quit");

                            Check.Equal("F2,F3,F4,Ctrl+Q", string.Join(",", Keys(resolver.Resolve(FocusPath.Build("m", outer)))), "default: inner first");
                            inner.HintOrder = KeyHintOrder.OwnFirst;
                            Check.Equal("F3,F2,F4,Ctrl+Q", string.Join(",", Keys(resolver.Resolve(FocusPath.Build("m", outer)))), "OwnFirst moves the container ahead of its child");
                            inner.Exclusive = true;
                            Check.Equal("F3,F2,Ctrl+Q", string.Join(",", Keys(resolver.Resolve(FocusPath.Build("m", outer)))), "Exclusive drops the outer container, keeps app hints");
                            outer.Exclusive = true;
                            Check.Equal("F3,F2,Ctrl+Q", string.Join(",", Keys(resolver.Resolve(FocusPath.Build("m", outer)))), "the innermost exclusive source decides");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("TypingHints", "StatusBarRightTextAndReserved", "RightText truncates before hints, and ReservedHint survives the narrowest width",
                        _ =>
                        {
                            StatusBar bar = new StatusBar
                            {
                                RightText = "connected",
                                ReservedHint = new KeyHint("F1", "Help"),
                                HintSource = () => new[] { new KeyHint("F1", "Help"), new KeyHint("Enter", "Open") }
                            };
                            string wide = Snapshot.RenderWidget(bar, 60, 1);
                            Check.True(wide.StartsWith("F1 Help", StringComparison.Ordinal), "reserved hint first");
                            Check.Equal(1, wide.Split(new[] { "F1" }, StringSplitOptions.None).Length - 1, "the duplicate F1 is not repeated");
                            Check.True(wide.EndsWith("connected", StringComparison.Ordinal), "right text at the end");

                            string tight = Snapshot.RenderWidget(bar, 26, 1);
                            Check.True(tight.Contains("Enter Open"), "hints keep their space");
                            Check.False(tight.Contains("connected"), "the right text is truncated first");

                            string tiny = Snapshot.RenderWidget(bar, 4, 1);
                            Check.True(tiny.StartsWith("F1", StringComparison.Ordinal), "the reserved hint survives at width 4");
                            Check.True(new StatusBar().RightText == null && new StatusBar().ReservedHint == null, "both default to null");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("TypingHints", "Compatibility", "A widget implementing only ITextEntry behaves as in 1.4.0, and With methods leave the original unchanged",
                        _ =>
                        {
                            KeyHint original = new KeyHint("?", "Help");
                            KeyHint marked = original.WhileTyping();
                            KeyHint alternative = original.WithTypingAlternative(KeyHint.For("f1", "Help"));
                            Check.False(original.WorksWhileTyping, "original not marked");
                            Check.True(original.TypingAlternative == null, "original has no alternative");
                            Check.True(marked.WorksWhileTyping && !ReferenceEquals(marked, original), "a new marked instance");
                            Check.Equal("F1", alternative.TypingAlternative!.Key, "a new instance with the alternative");
                            Check.Equal("?", alternative.Key, "keeps its own key");

                            ComboBox combo = new ComboBox(new[] { "one", "two" });
                            HintContainer container = new HintContainer(combo) { Hints = new[] { KeyHint.For("enter", "Pick"), KeyHint.For("up", "Prev") } };
                            List<string> keys = Keys(new KeyHintResolver().Resolve(FocusPath.Build("c", container)));
                            Check.True(keys.Contains("Enter") && keys.Contains("Up"), "a plain ITextEntry hides only printable keys");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("TypingHints", "Guards", "A typing alternative that would type, and a null alternative, are rejected",
                        _ =>
                        {
                            KeyHint help = new KeyHint("?", "Help");
                            Check.Throws<ArgumentException>(() => help.WithTypingAlternative(new KeyHint("h", "Help")), "a letter would type");
                            Check.Throws<ArgumentException>(() => help.WithTypingAlternative(KeyHint.For("shift+a", "Help")), "a shifted letter would type");
                            Check.Throws<ArgumentNullException>(() => help.WithTypingAlternative(null!), "null alternative");
                            Check.Equal("Ctrl+H", help.WithTypingAlternative(KeyHint.For("ctrl+h", "Help")).TypingAlternative!.Key, "a control chord is fine");
                            return Task.CompletedTask;
                        })
                });
        }

        private static List<string> Keys(IReadOnlyList<KeyHint> hints)
        {
            return hints.Select(h => h.Key).ToList();
        }
    }
}

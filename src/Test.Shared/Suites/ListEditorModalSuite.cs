namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Modals;

    /// <summary>
    /// Coverage for <see cref="ListEditorModal{T}"/>: inline add with live preview and validation,
    /// remove, reorder, finish/cancel semantics, and argument guards.
    /// </summary>
    public static class ListEditorModalSuite
    {
        /// <summary>
        /// Builds the list-editor modal suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "ListEditorModal",
                displayName: "ListEditorModal (validated list editor)",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("ListEditorModal", "AddCommitsParsedItem", "Typing then Enter appends the parsed value",
                        async _ =>
                        {
                            ListEditorModal<string> modal = NewEditor(Array.Empty<string>());
                            AddItem(modal, "xy");
                            Check.Equal(1, modal.Count, "one item added");
                            modal.HandleKey(KeyEvent.Special(KeyCode.Enter)); // finish
                            IReadOnlyList<string> result = await Finish(modal).ConfigureAwait(false);
                            Check.Equal("xy", result[0], "the parsed value");
                        }),

                    new TestCaseDescriptor("ListEditorModal", "AddMultiple", "Two adds yield both in entry order",
                        async _ =>
                        {
                            ListEditorModal<string> modal = NewEditor(Array.Empty<string>());
                            AddItem(modal, "a");
                            AddItem(modal, "b");
                            modal.HandleKey(KeyEvent.Special(KeyCode.Enter));
                            IReadOnlyList<string> result = await Finish(modal).ConfigureAwait(false);
                            Check.Equal(2, result.Count, "two items");
                            Check.Equal("a", result[0], "first");
                            Check.Equal("b", result[1], "second");
                        }),

                    new TestCaseDescriptor("ListEditorModal", "DescribePreviewShown", "While editing, the describe preview renders",
                        _ =>
                        {
                            ListEditorModal<string> modal = NewEditor(Array.Empty<string>());
                            modal.HandleKey(KeyEvent.Char('a')); // enter add mode
                            modal.HandleKey(KeyEvent.Char('h'));
                            modal.HandleKey(KeyEvent.Char('i'));
                            Check.True(RenderContains(modal, "→"), "live preview arrow shown");
                            Check.True(RenderContains(modal, "is hi"), "describe text shown");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ListEditorModal", "RemoveSelected", "Remove drops the selected row",
                        async _ =>
                        {
                            ListEditorModal<string> modal = NewEditor(new[] { "a", "b", "c" });
                            modal.HandleKey(KeyEvent.Char('d')); // remove selected (a)
                            Check.Equal(2, modal.Count, "one removed");
                            modal.HandleKey(KeyEvent.Special(KeyCode.Enter));
                            IReadOnlyList<string> result = await Finish(modal).ConfigureAwait(false);
                            Check.Equal("b", result[0], "a is gone");
                            Check.Equal("c", result[1], "c remains");
                        }),

                    new TestCaseDescriptor("ListEditorModal", "ReorderWhenEnabled", "Alt+Up reorders when AllowReorder is set",
                        async _ =>
                        {
                            ListEditorOptions<string> options = DefaultOptions();
                            options.AllowReorder = true;
                            ListEditorModal<string> modal = new ListEditorModal<string>(new[] { "a", "b" }, s => s, options);
                            modal.HandleKey(KeyEvent.Special(KeyCode.Down)); // select b
                            modal.HandleKey(KeyEvent.Special(KeyCode.Up, KeyModifiers.Alt)); // move b up
                            modal.HandleKey(KeyEvent.Special(KeyCode.Enter));
                            IReadOnlyList<string> result = await Finish(modal).ConfigureAwait(false);
                            Check.Equal("b", result[0], "b moved to the top");
                            Check.Equal("a", result[1], "a moved down");
                        }),

                    new TestCaseDescriptor("ListEditorModal", "FinishReturnsList", "Enter in Browse closes with the items in order",
                        async _ =>
                        {
                            ListEditorModal<string> modal = NewEditor(new[] { "x", "y" });
                            modal.HandleKey(KeyEvent.Special(KeyCode.Enter));
                            IReadOnlyList<string> result = await Finish(modal).ConfigureAwait(false);
                            Check.Equal(2, result.Count, "both returned");
                            Check.Equal("x", result[0], "order preserved");
                            return;
                        }),

                    new TestCaseDescriptor("ListEditorModal", "CancelReturnsNull", "Escape in Browse closes with null, not an empty list",
                        async _ =>
                        {
                            ListEditorModal<string> modal = NewEditor(new[] { "x" });
                            modal.HandleKey(KeyEvent.Special(KeyCode.Escape));
                            object? result = await modal.Completion.ConfigureAwait(false);
                            Check.True(result == null, "cancel yields null");
                        }),

                    new TestCaseDescriptor("ListEditorModal", "InvalidRejectedBufferKept", "A parse failure shows the error, adds nothing, and keeps the buffer",
                        _ =>
                        {
                            ListEditorModal<string> modal = NewEditor(Array.Empty<string>());
                            modal.HandleKey(KeyEvent.Char('a'));
                            modal.HandleKey(KeyEvent.Char('b'));
                            modal.HandleKey(KeyEvent.Char('a'));
                            modal.HandleKey(KeyEvent.Char('d'));
                            modal.HandleKey(KeyEvent.Special(KeyCode.Enter)); // commit "bad" -> failure
                            Check.Equal(0, modal.Count, "nothing added");
                            Check.True(modal.IsAdding, "still in add mode");
                            Check.True(RenderContains(modal, "✗"), "error marker shown");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ListEditorModal", "DuplicateRejected", "A duplicate is refused when AllowDuplicates is false",
                        _ =>
                        {
                            ListEditorModal<string> modal = NewEditor(new[] { "a" });
                            modal.HandleKey(KeyEvent.Char('a')); // add mode
                            modal.HandleKey(KeyEvent.Char('a')); // type "a"
                            modal.HandleKey(KeyEvent.Special(KeyCode.Enter)); // commit -> duplicate
                            Check.Equal(1, modal.Count, "no duplicate added");
                            Check.True(modal.IsAdding, "still in add mode after refusal");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ListEditorModal", "EscInEditReturnsToBrowse", "Escape while adding cancels the add, not the modal",
                        _ =>
                        {
                            ListEditorModal<string> modal = NewEditor(Array.Empty<string>());
                            modal.HandleKey(KeyEvent.Char('a'));
                            modal.HandleKey(KeyEvent.Char('z'));
                            modal.HandleKey(KeyEvent.Special(KeyCode.Escape));
                            Check.False(modal.IsAdding, "left add mode");
                            Check.False(modal.IsClosed, "modal stays open");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ListEditorModal", "AddDisabledWhenNoParser", "With no parser the add chord is a no-op",
                        _ =>
                        {
                            ListEditorOptions<string> options = new ListEditorOptions<string>();
                            ListEditorModal<string> modal = new ListEditorModal<string>(new[] { "a" }, s => s, options);
                            modal.HandleKey(KeyEvent.Char('a'));
                            Check.False(modal.IsAdding, "add mode not entered");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ListEditorModal", "EmptyDisallowedBlocksFinish", "With AllowEmpty false and zero items, Enter does not close",
                        _ =>
                        {
                            ListEditorOptions<string> options = DefaultOptions();
                            options.AllowEmpty = false;
                            ListEditorModal<string> modal = new ListEditorModal<string>(Array.Empty<string>(), s => s, options);
                            modal.HandleKey(KeyEvent.Special(KeyCode.Enter));
                            Check.False(modal.IsClosed, "finish blocked while empty");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ListEditorModal", "NullArgsThrow", "Null initialItems or display throws",
                        _ =>
                        {
                            Check.Throws<ArgumentNullException>(() => new ListEditorModal<string>(null!, s => s), "null items");
                            Check.Throws<ArgumentNullException>(() => new ListEditorModal<string>(Array.Empty<string>(), null!), "null display");
                            return Task.CompletedTask;
                        })
                });
        }

        private static ListEditorOptions<string> DefaultOptions()
        {
            ListEditorOptions<string> options = new ListEditorOptions<string>();
            options.Parse = text =>
            {
                if (string.IsNullOrEmpty(text))
                    return ParseResult<string>.Failure("Empty.");
                if (text == "bad")
                    return ParseResult<string>.Failure("Bad value.");
                return ParseResult<string>.Success(text);
            };
            options.Describe = value => "is " + value;
            return options;
        }

        private static ListEditorModal<string> NewEditor(IEnumerable<string> items)
        {
            return new ListEditorModal<string>(items, s => s, DefaultOptions());
        }

        private static void AddItem(ListEditorModal<string> modal, string text)
        {
            modal.HandleKey(KeyEvent.Char('a'));
            for (int i = 0; i < text.Length; i++)
                modal.HandleKey(KeyEvent.Char(text[i]));
            modal.HandleKey(KeyEvent.Special(KeyCode.Enter));
        }

        private static async Task<IReadOnlyList<string>> Finish(ListEditorModal<string> modal)
        {
            object? result = await modal.Completion.ConfigureAwait(false);
            return (IReadOnlyList<string>)result!;
        }

        private static bool RenderContains(ListEditorModal<string> modal, string needle)
        {
            CellBuffer buffer = new CellBuffer(48, 18);
            modal.Render(new BufferSurface(buffer));
            for (int y = 0; y < buffer.Height; y++)
            {
                StringBuilder row = new StringBuilder(buffer.Width);
                for (int x = 0; x < buffer.Width; x++)
                    row.Append(buffer.Get(x, y).Grapheme);

                if (row.ToString().Contains(needle))
                    return true;
            }

            return false;
        }
    }
}

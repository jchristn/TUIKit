namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Modals;
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for the multi-select surfaces: the <see cref="CheckList{T}"/> widget and the
    /// <see cref="MultiSelectModal{T}"/> dialog, including argument guards.
    /// </summary>
    public static class MultiSelectSuite
    {
        /// <summary>
        /// Builds the multi-select suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "MultiSelect",
                displayName: "Multi-Select (CheckList / MultiSelectModal)",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("MultiSelect", "ToggleSpace", "Space toggles the item under the cursor",
                        _ =>
                        {
                            CheckList<string> list = new CheckList<string>(new[] { "one", "two", "three" });
                            list.HandleKey(KeyEvent.Special(KeyCode.Down)); // cursor -> index 1
                            list.HandleKey(KeyEvent.Char(' '));             // toggle index 1
                            IReadOnlyList<int> checkedIndices = list.CheckedIndices;
                            Check.Equal(1, checkedIndices.Count, "one item checked");
                            Check.Equal(1, checkedIndices[0], "index 1 checked");
                            Check.True(list.IsChecked(1), "IsChecked agrees");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MultiSelect", "ToggleAll", "The toggle-all key checks all then clears all",
                        _ =>
                        {
                            CheckList<string> list = new CheckList<string>(new[] { "a", "b", "c" });
                            list.HandleKey(KeyEvent.Char('a'));
                            Check.Equal(3, list.CheckedIndices.Count, "all checked");
                            list.HandleKey(KeyEvent.Char('a'));
                            Check.Equal(0, list.CheckedIndices.Count, "all cleared");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MultiSelect", "DisplaySelector", "A non-string item type renders through its selector",
                        _ =>
                        {
                            CheckList<int> list = new CheckList<int>(new[] { 7, 8 }, i => "#" + i);
                            string text = TUIKit.Testing.Snapshot.RenderWidget(list, 12, 2);
                            Check.True(text.Contains("#7"), "selector label rendered");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MultiSelect", "ModalAccept", "Enter completes with the checked indices",
                        async ct =>
                        {
                            MultiSelectModal<string> modal = new MultiSelectModal<string>(
                                "Pick", new List<string> { "red", "green", "blue" });
                            modal.List.SetChecked(0, true);
                            modal.List.SetChecked(2, true);
                            modal.HandleKey(KeyEvent.Special(KeyCode.Enter));

                            object? result = await modal.Completion.ConfigureAwait(false);
                            IReadOnlyList<int> indices = (IReadOnlyList<int>)result!;
                            Check.Equal(2, indices.Count, "two checked");
                            Check.Equal(0, indices[0], "first checked index");
                            Check.Equal(2, indices[1], "second checked index");
                        }),

                    new TestCaseDescriptor("MultiSelect", "ModalCancel", "Escape completes with null",
                        async ct =>
                        {
                            MultiSelectModal<string> modal = new MultiSelectModal<string>(
                                "Pick", new List<string> { "red", "green" });
                            modal.HandleKey(KeyEvent.Special(KeyCode.Escape));
                            object? result = await modal.Completion.ConfigureAwait(false);
                            Check.True(result == null, "cancel yields null");
                        }),

                    new TestCaseDescriptor("MultiSelect", "ModalRendersBox", "The modal draws a bordered box",
                        _ =>
                        {
                            MultiSelectModal<string> modal = new MultiSelectModal<string>(
                                "Options", new List<string> { "one", "two" });
                            CellBuffer buffer = new CellBuffer(40, 12);
                            modal.Render(new BufferSurface(buffer));

                            bool sawBorder = false;
                            for (int y = 0; y < buffer.Height && !sawBorder; y++)
                            {
                                for (int x = 0; x < buffer.Width; x++)
                                {
                                    string glyph = buffer.Get(x, y).Grapheme;
                                    if (glyph == "╭" || glyph == "┌")
                                    {
                                        sawBorder = true;
                                        break;
                                    }
                                }
                            }

                            Check.True(sawBorder, "border glyph present");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MultiSelect", "GuardNullItems", "CheckList rejects null items",
                        _ =>
                        {
                            Check.Throws<ArgumentNullException>(() => new CheckList<string>(null!), "null items");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MultiSelect", "GuardNullSelector", "CheckList requires a selector for a non-string type",
                        _ =>
                        {
                            Check.Throws<ArgumentNullException>(() => new CheckList<int>(new[] { 1, 2 }), "null selector for int");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MultiSelect", "GuardRange", "IsChecked and SetChecked reject out-of-range indices",
                        _ =>
                        {
                            CheckList<string> list = new CheckList<string>(new[] { "x" });
                            Check.Throws<ArgumentOutOfRangeException>(() => list.IsChecked(5), "IsChecked out of range");
                            Check.Throws<ArgumentOutOfRangeException>(() => list.SetChecked(-1, true), "SetChecked out of range");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MultiSelect", "GuardModalArgs", "The modal rejects null and empty arguments",
                        _ =>
                        {
                            Check.Throws<ArgumentNullException>(() => new MultiSelectModal<string>(null!, new List<string> { "a" }), "null title");
                            Check.Throws<ArgumentNullException>(() => new MultiSelectModal<string>("t", null!), "null options");
                            Check.Throws<ArgumentException>(() => new MultiSelectModal<string>("t", new List<string>()), "empty options");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}

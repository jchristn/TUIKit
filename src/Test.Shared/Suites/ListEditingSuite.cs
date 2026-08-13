namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit.Input;
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for the list-editing widgets: <see cref="ActionListView{T}"/> row actions and
    /// <see cref="ReorderableList{T}"/> reordering and removal.
    /// </summary>
    public static class ListEditingSuite
    {
        /// <summary>
        /// Builds the list-editing suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "ListEditing",
                displayName: "List Editing (ActionListView / ReorderableList)",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("ListEditing", "RowAction", "A registered chord fires with the right row and action id",
                        _ =>
                        {
                            ActionListView<string> list = new ActionListView<string>();
                            list.SetItems(new[] { "a", "b", "c" });
                            list.RegisterAction(KeyChord.Parse("d"), "delete");

                            ListAction<string>? fired = null;
                            list.Activated += a => fired = a;

                            list.HandleKey(KeyEvent.Special(KeyCode.Down)); // select "b"
                            list.HandleKey(KeyEvent.Char('d'));

                            Check.True(fired != null, "action fired");
                            Check.Equal(1, fired!.Index, "row index");
                            Check.Equal("b", fired.Item, "row item");
                            Check.Equal("delete", fired.ActionId, "action id");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ListEditing", "ActivateEnter", "Enter fires the built-in activate action",
                        _ =>
                        {
                            ActionListView<string> list = new ActionListView<string>();
                            list.SetItems(new[] { "x" });
                            string? action = null;
                            list.Activated += a => action = a.ActionId;
                            list.HandleKey(KeyEvent.Special(KeyCode.Enter));
                            Check.Equal(ActionListView<string>.ActivateActionId, action, "activate id");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ListEditing", "DisabledRow", "A disabled action does not fire on an ineligible row",
                        _ =>
                        {
                            ActionListView<string> list = new ActionListView<string>();
                            list.SetItems(new[] { "keep", "lock" });
                            list.RegisterAction(KeyChord.Parse("d"), "delete", item => item != "lock");

                            int count = 0;
                            list.Activated += _2 => count++;
                            list.HandleKey(KeyEvent.Special(KeyCode.Down)); // select "lock"
                            list.HandleKey(KeyEvent.Char('d'));             // disabled -> no fire
                            Check.Equal(0, count, "disabled row swallowed the action");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ListEditing", "ActionGuards", "RegisterAction rejects a null or empty action id",
                        _ =>
                        {
                            ActionListView<string> list = new ActionListView<string>();
                            Check.Throws<ArgumentException>(() => list.RegisterAction(KeyChord.Parse("x"), ""), "empty action id");
                            Check.Throws<ArgumentException>(() => list.RegisterAction(KeyChord.Parse("x"), null!), "null action id");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ListEditing", "Reorder", "Move up and down reorder and clamp at the ends",
                        _ =>
                        {
                            ReorderableList<string> list = new ReorderableList<string>(new[] { "a", "b", "c" });
                            Check.False(list.MoveUp(), "cannot move first item up");
                            list.HandleKey(KeyEvent.Special(KeyCode.Down)); // select "b"
                            Check.True(list.MoveUp(), "moved b up");
                            Check.Equal("b", list.Order[0], "b now first");
                            Check.Equal("a", list.Order[1], "a now second");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ListEditing", "RemoveAndClamp", "Delete removes the selected item and fixes selection",
                        _ =>
                        {
                            ReorderableList<string> list = new ReorderableList<string>(new[] { "a", "b" });
                            string? removed = null;
                            list.Removed += r => removed = r;
                            list.HandleKey(KeyEvent.Special(KeyCode.Down)); // select "b" (last)
                            list.HandleKey(KeyEvent.Special(KeyCode.Delete));
                            Check.Equal("b", removed, "removed the selected item");
                            Check.Equal(1, list.Order.Count, "one item left");
                            Check.Equal(0, list.SelectedIndex, "selection clamped");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ListEditing", "ReorderGuards", "ReorderableList rejects null items",
                        _ =>
                        {
                            Check.Throws<ArgumentNullException>(() => new ReorderableList<string>(null!), "null items");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}

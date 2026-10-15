namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for 1.9 item C6 dirty tracking: <see cref="Form.DirtyTracking"/>, <see cref="FormDirtyTracking"/>,
    /// <see cref="IValueSnapshot"/> on the field widgets, and <see cref="Form.SetFieldSnapshot"/>.
    /// </summary>
    public static class FormDirtySuite
    {
        private const string _Id = "FormDirty";

        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: _Id,
                displayName: "Form Dirty Tracking",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(_Id, "EventsDefault", "Events mode keeps the 1.7 behavior: type then delete stays dirty",
                        _ =>
                        {
                            Form form = new Form();
                            TextField name = form.Add("Name", new TextField());
                            Check.Equal(FormDirtyTracking.Events, form.DirtyTracking, "default");
                            name.HandleKey(KeyEvent.Char('a'));
                            name.HandleKey(KeyEvent.Special(KeyCode.Backspace));
                            Check.True(form.IsDirty, "still dirty after a revert");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "SnapshotRevert", "Snapshot mode: type then delete returns to clean and raises DirtyChanged(false)",
                        _ =>
                        {
                            Form form = new Form();
                            TextField name = form.Add("Name", new TextField { Value = "ada" });
                            form.DirtyTracking = FormDirtyTracking.Snapshot;
                            List<bool> changes = new List<bool>();
                            form.DirtyChanged += d => changes.Add(d);
                            form.HandleKey(KeyEvent.Char('x'));
                            Check.True(form.IsDirty, "dirty");
                            form.HandleKey(KeyEvent.Special(KeyCode.Backspace));
                            Check.False(form.IsDirty, "clean after the revert");
                            Check.Equal("True,False", string.Join(",", changes), "DirtyChanged both ways");
                            name.Value = "grace";
                            form.MarkClean();
                            Check.False(form.IsDirty, "MarkClean records the new value");
                            name.Value = "ada";
                            Check.True(form.IsDirty, "the old value now differs");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Widgets", "Each listed widget implements IValueSnapshot with a stable value",
                        _ =>
                        {
                            Checkbox box = new Checkbox("x", true);
                            Check.Equal("1", box.GetSnapshot(), "Checkbox");
                            RadioGroup radio = new RadioGroup(new[] { "a", "b" });
                            Check.Equal("0", radio.GetSnapshot(), "RadioGroup");
                            Dropdown<string> drop = new Dropdown<string>(new[] { "a", "b" });
                            Check.Equal(drop.SelectedIndex.ToString(), drop.GetSnapshot(), "Dropdown");
                            ComboBox combo = new ComboBox(new[] { "a" }) { Value = "z" };
                            Check.Equal("z", combo.GetSnapshot(), "ComboBox");
                            CheckList<string> list = new CheckList<string>(new[] { "a", "b", "c" });
                            list.SetChecked(0, true);
                            list.SetChecked(2, true);
                            Check.Equal("0,2", list.GetSnapshot(), "CheckList");
                            Check.Equal("t", new TextEditor { Text = "t" }.GetSnapshot(), "TextEditor");
                            Check.Equal("v", new TextField { Value = "v" }.GetSnapshot(), "TextField");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "CheckListWithoutEvents", "A CheckList (no change events) still updates IsDirty after a key through the form",
                        _ =>
                        {
                            Form form = new Form { DirtyTracking = FormDirtyTracking.Snapshot };
                            form.Add("Tags", new CheckList<string>(new[] { "a", "b" }));
                            form.HandleKey(KeyEvent.Char(' '));
                            Check.True(form.IsDirty, "dirty after a toggle");
                            form.HandleKey(KeyEvent.Char(' '));
                            Check.False(form.IsDirty, "clean after toggling back");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Fallbacks", "A field without IValueSnapshot falls back to events; SetFieldSnapshot covers a custom field",
                        _ =>
                        {
                            Form form = new Form { DirtyTracking = FormDirtyTracking.Snapshot };
                            ToggleField custom = new ToggleField();
                            form.Add("Custom", custom);
                            custom.Toggle();
                            Check.True(form.IsDirty, "event fallback");
                            custom.Toggle();
                            Check.True(form.IsDirty, "events cannot see the revert");
                            form.SetFieldSnapshot(0, () => custom.On ? "on" : "off");
                            Check.False(form.IsDirty, "snapshot supplied: clean at the current value");
                            custom.Toggle();
                            Check.True(form.IsDirty, "dirty");
                            custom.Toggle();
                            Check.False(form.IsDirty, "the revert is seen");
                            Check.Throws<ArgumentOutOfRangeException>(() => form.SetFieldSnapshot(4, null), "bad index");
                            form.TrackChanges = false;
                            custom.Toggle();
                            Check.False(form.IsDirty, "TrackChanges false keeps it clean");
                            return Task.CompletedTask;
                        }),
                });
        }
    }
}

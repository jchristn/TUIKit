namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Widgets;

    /// <summary>
    /// Touchstone suite covering the mouse support added to the previously keyboard-only widgets: text input
    /// caret placement, single-select and check toggling in the list widgets, form field focus and forwarding,
    /// collapsible toggling, the color channel picker, and split-view pane routing.
    /// </summary>
    public static class WidgetMouseCoverageSuite
    {
        /// <summary>
        /// Builds the widget mouse coverage suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "WidgetMouseCoverage",
                displayName: "Widget Mouse Coverage",
                cases: new List<TestCaseDescriptor>
                {
                    Case("TextFieldClickPositionsCaret", "A click positions the caret at the clicked column", _ =>
                    {
                        TextField field = new TextField { Value = "hello" };
                        Check.True(field.HandleMouse(Press(2, 0)), "left press consumed");
                        field.HandleKey(Char('X'));
                        Check.Equal("heXllo", field.Value, "typed at the clicked caret column");
                        Check.False(field.HandleMouse(Motion(MouseEventKind.Move, 1, 0)), "move not consumed");
                        return Task.CompletedTask;
                    }),

                    Case("TextEditorClickPositionsCaret", "A click positions the caret at the clicked row and column", _ =>
                    {
                        TextEditor editor = new TextEditor();
                        editor.Text = "ab\ncd";
                        editor.Render(new BufferSurface(new CellBuffer(20, 5)));
                        Check.True(editor.HandleMouse(Press(1, 1)), "left press consumed");
                        Check.Equal(1, editor.CaretRow, "caret row");
                        Check.Equal(1, editor.CaretColumn, "caret column");
                        return Task.CompletedTask;
                    }),

                    Case("RadioGroupClickSelects", "A click selects the option on the clicked row", _ =>
                    {
                        RadioGroup group = new RadioGroup(new[] { "r", "g", "b" });
                        Check.True(group.HandleMouse(Press(3, 2)), "left press consumed");
                        Check.Equal(2, group.SelectedIndex, "third option selected");
                        Check.True(group.HandleMouse(Wheel(MouseButton.WheelUp)), "wheel consumed");
                        Check.Equal(1, group.SelectedIndex, "wheel moved up");
                        return Task.CompletedTask;
                    }),

                    Case("CheckListClickSelectsAndToggles", "A click selects and toggles the row", _ =>
                    {
                        CheckList<string> list = new CheckList<string>(new[] { "a", "b", "c" });
                        list.Render(new BufferSurface(new CellBuffer(30, 10)));
                        Check.True(list.HandleMouse(Press(1, 1)), "left press consumed");
                        Check.Equal(1, list.SelectedIndex, "second row selected");
                        Check.True(list.IsChecked(1), "second row toggled on");
                        return Task.CompletedTask;
                    }),

                    Case("FuzzyListClickSelects", "A click selects the result on the clicked row (below the query line)", _ =>
                    {
                        FuzzyList<string> list = new FuzzyList<string>(new[] { "a", "b", "c" });
                        list.Render(new BufferSurface(new CellBuffer(30, 10)));
                        Check.True(list.HandleMouse(Press(1, 2)), "left press consumed");
                        Check.Equal("b", list.SelectedItem, "second result selected");
                        return Task.CompletedTask;
                    }),

                    Case("CollapsibleHeaderClickToggles", "Clicking the header toggles the section", _ =>
                    {
                        Collapsible section = new Collapsible("head", new Label(Text.From("body")));
                        Check.True(section.Expanded, "starts expanded");
                        Check.True(section.HandleMouse(Press(0, 0)), "header press consumed");
                        Check.False(section.Expanded, "collapsed after click");
                        return Task.CompletedTask;
                    }),

                    Case("ColorPickerClickSelectsChannelAndValue", "Clicking a channel row selects it and sets its value from x", _ =>
                    {
                        ColorPicker picker = new ColorPicker();
                        picker.Render(new BufferSurface(new CellBuffer(40, 6)));
                        // Row 1 is the G channel; click near the right end of its bar.
                        Check.True(picker.HandleMouse(Press(34, 1)), "left press consumed");
                        Check.True(picker.ActiveValue >= 240, "green channel driven high by the click");
                        return Task.CompletedTask;
                    }),

                    Case("FormClickFocusesField", "Clicking a field focuses it and forwards to the widget", _ =>
                    {
                        Form form = new Form();
                        TextField a = form.Add("A", new TextField { Value = "aaa" });
                        TextField b = form.Add("B", new TextField { Value = "bbb" });
                        form.Render(new BufferSurface(new CellBuffer(40, 12)));
                        // Field 0 occupies rows 0..2 (label, widget, gap); field 1's widget is on row 4.
                        Check.True(form.HandleMouse(Press(3, 4)), "press on field B consumed");
                        Check.Equal(1, form.FocusedIndex, "field B focused");
                        b.HandleKey(Char('Z'));
                        Check.True(b.Value.Contains("Z"), "keystroke went to the focused field");
                        return Task.CompletedTask;
                    }),

                    Case("SplitViewRoutesToPane", "A click routes to the pane under the pointer", _ =>
                    {
                        RadioGroup left = new RadioGroup(new[] { "0", "1", "2" });
                        RadioGroup right = new RadioGroup(new[] { "0", "1", "2" });
                        SplitView split = new SplitView(SplitOrientation.Horizontal, left, right, 0.5) { ShowDivider = false };
                        split.Render(new BufferSurface(new CellBuffer(40, 10)));
                        // First pane is columns 0..19; click x=25 lands in the right pane at local x=5, y=2.
                        Check.True(split.HandleMouse(Press(25, 2)), "press consumed by the right pane");
                        Check.Equal(2, right.SelectedIndex, "right pane's third option selected");
                        Check.Equal(0, left.SelectedIndex, "left pane untouched");
                        return Task.CompletedTask;
                    })
                });
        }

        private static TestCaseDescriptor Case(string id, string name, System.Func<System.Threading.CancellationToken, Task> body)
        {
            return new TestCaseDescriptor("WidgetMouseCoverage", id, name, body);
        }

        private static MouseEvent Press(int x, int y)
        {
            return new MouseEvent(MouseEventKind.Press, MouseButton.Left, x, y, KeyModifiers.None, 1);
        }

        private static MouseEvent Motion(MouseEventKind kind, int x, int y)
        {
            return new MouseEvent(kind, MouseButton.None, x, y, KeyModifiers.None, 0);
        }

        private static MouseEvent Wheel(MouseButton button)
        {
            return new MouseEvent(MouseEventKind.Wheel, button, 0, 0, KeyModifiers.None, 0);
        }

        private static KeyEvent Char(char c)
        {
            return KeyEvent.Char(c);
        }
    }
}

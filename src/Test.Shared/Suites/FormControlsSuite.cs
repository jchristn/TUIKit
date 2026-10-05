namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Hosting;
    using TUIKit.Input;
    using TUIKit.Layout;
    using TUIKit.Modals;
    using TUIKit.Terminal;
    using TUIKit.Testing;
    using TUIKit.Widgets;

    /// <summary>
    /// Touchstone suite covering the new controls (U4): Button, ButtonRow (wrapping), Dropdown, ComboBox,
    /// ContextMenu, Tooltip, Badge, and the Form upgrades (hidden rows, disabled rows, dirty tracking,
    /// checkbox fields).
    /// </summary>
    public static class FormControlsSuite
    {
        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "FormControls",
                displayName: "Buttons, Pickers, Menus, Tooltips, Badges, Forms",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("FormControls", "Button", "Button clicks on Enter, Space, and the mouse, not while disabled",
                        _ =>
                        {
                            int clicks = 0;
                            Button button = new Button("Save", () => clicks++);
                            button.HandleKey(KeyEvent.Special(KeyCode.Enter));
                            button.HandleKey(KeyEvent.Char(' '));
                            button.HandleMouse(new MouseEvent(MouseEventKind.Press, MouseButton.Left, 1, 0, KeyModifiers.None, 1));
                            Check.Equal(3, clicks, "three activations");
                            Check.True(Snapshot.RenderWidget(button, 12, 1).Contains("[ Save ]"), "rendered");
                            button.IsEnabled = false;
                            Check.False(button.HandleKey(KeyEvent.Special(KeyCode.Enter)), "disabled");
                            Check.Equal(3, clicks, "no click while disabled");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FormControls", "ButtonRowWraps", "ButtonRow wraps onto a second line and moves focus",
                        _ =>
                        {
                            ButtonRow row = new ButtonRow();
                            string clicked = string.Empty;
                            row.Add("Alpha", () => clicked = "a");
                            row.Add("Bravo", () => clicked = "b");
                            row.Add("Charlie", () => clicked = "c");
                            Check.Equal(1, row.LineCount(60), "one line when wide");
                            Check.Equal(2, row.LineCount(20), "wraps when narrow");
                            string text = Snapshot.RenderWidget(row, 20, 2);
                            Check.True(text.Split('\n')[1].Contains("Charlie"), "third button on the second line");
                            row.OnFocusChanged(true);
                            row.HandleKey(KeyEvent.Special(KeyCode.Right));
                            row.HandleKey(KeyEvent.Special(KeyCode.Enter));
                            Check.Equal("b", clicked, "Right then Enter clicks the second button");
                            Check.True(row.HandleKey(KeyEvent.Special(KeyCode.Tab)), "Tab moves within the row");
                            Check.False(row.HandleKey(KeyEvent.Special(KeyCode.Tab)), "Tab bubbles after the last button");
                            row.HandleMouse(new MouseEvent(MouseEventKind.Press, MouseButton.Left, 1, 0, KeyModifiers.None, 1));
                            Check.Equal("a", clicked, "click on the first button");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FormControls", "Dropdown", "Dropdown opens, navigates, picks, and raises SelectionChanged",
                        _ =>
                        {
                            Dropdown<string> drop = new Dropdown<string>(new[] { "Low", "Medium", "High" }) { Placeholder = "Pick" };
                            List<int> picks = new List<int>();
                            drop.SelectionChanged += (s, e) => picks.Add(e.NewValue);
                            Check.True(Snapshot.RenderWidget(drop, 12, 1).Contains("Pick"), "placeholder");
                            drop.HandleKey(KeyEvent.Special(KeyCode.Enter));
                            Check.True(drop.IsOpen, "opened");
                            Check.Equal(4, drop.Measure(new Size(12, 10)).Height, "measured height grows while open");
                            drop.HandleKey(KeyEvent.Char('h'));
                            drop.HandleKey(KeyEvent.Special(KeyCode.Enter));
                            Check.False(drop.IsOpen, "closed after picking");
                            Check.Equal("High", drop.SelectedItem, "type-to-jump picked High");
                            drop.HandleKey(KeyEvent.Special(KeyCode.Up));
                            Check.Equal("Medium", drop.SelectedItem, "Up steps while closed");
                            drop.HandleKey(KeyEvent.Special(KeyCode.Enter));
                            drop.HandleKey(KeyEvent.Special(KeyCode.Down));
                            drop.HandleKey(KeyEvent.Special(KeyCode.Escape));
                            Check.Equal("Medium", drop.SelectedItem, "Escape cancels");
                            drop.SelectedIndex = 0;
                            Check.Equal("2,1,0", string.Join(",", picks), "events for every change");
                            drop.IsEnabled = false;
                            Check.False(drop.HandleKey(KeyEvent.Special(KeyCode.Enter)), "disabled");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FormControls", "DropdownMouse", "Dropdown opens and picks with the mouse",
                        _ =>
                        {
                            Dropdown<string> drop = new Dropdown<string>(new[] { "a", "b", "c" });
                            drop.HandleMouse(new MouseEvent(MouseEventKind.Press, MouseButton.Left, 0, 0, KeyModifiers.None, 1));
                            Snapshot.RenderWidget(drop, 10, 4);
                            drop.HandleMouse(new MouseEvent(MouseEventKind.Press, MouseButton.Left, 1, 2, KeyModifiers.None, 1));
                            Check.Equal("b", drop.SelectedItem, "picked the second row");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FormControls", "ComboBox", "ComboBox filters while typing and accepts a suggestion",
                        _ =>
                        {
                            ComboBox combo = new ComboBox(new[] { "alpha", "beta", "alphabet" });
                            List<string> values = new List<string>();
                            combo.ValueChanged += (s, e) => values.Add(e.NewValue);
                            combo.HandleKey(KeyEvent.Char('a'));
                            combo.HandleKey(KeyEvent.Char('l'));
                            Check.True(combo.IsOpen, "open while matching");
                            Check.Equal(2, combo.Filtered.Count, "two matches");
                            combo.HandleKey(KeyEvent.Special(KeyCode.Down));
                            combo.HandleKey(KeyEvent.Special(KeyCode.Enter));
                            Check.Equal("alphabet", combo.Value, "accepted the highlighted match");
                            Check.Equal("a,al,alphabet", string.Join(",", values), "value events");
                            combo.AllowFreeText = false;
                            combo.Value = "alpha";
                            combo.HandleKey(KeyEvent.Char('z'));
                            combo.OnFocusChanged(false);
                            Check.Equal("alpha", combo.Value, "restricted value reverted");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FormControls", "ContextMenu", "ContextMenu navigates, skips disabled and separators, and runs the pick",
                        _ =>
                        {
                            string ran = string.Empty;
                            List<MenuItem> items = new List<MenuItem>
                            {
                                new MenuItem("Open", () => ran = "open"),
                                new MenuItem(ContextMenu.Separator),
                                new MenuItem("Delete", () => ran = "delete") { Enabled = false },
                                new MenuItem("Copy", () => ran = "copy")
                            };
                            ContextMenu menu = new ContextMenu(items, 70, 20);
                            CellBuffer screen = new CellBuffer(40, 10);
                            menu.Render(new BufferSurface(screen));
                            Check.True(Snapshot.ToText(screen).Contains("Copy"), "clamped onto the screen");
                            menu.HandleKey(KeyEvent.Special(KeyCode.Down));
                            Check.Equal(3, menu.HighlightedIndex, "skipped separator and disabled");
                            menu.HandleKey(KeyEvent.Special(KeyCode.Enter));
                            Check.Equal("copy", ran, "action ran");
                            Check.Equal(3, (int)menu.Completion.Result!, "result index");

                            ContextMenu other = new ContextMenu(items, 0, 0);
                            other.Render(new BufferSurface(new CellBuffer(40, 10)));
                            other.HandleMouse(new MouseEvent(MouseEventKind.Press, MouseButton.Left, 39, 9, KeyModifiers.None, 1));
                            Check.Equal(-1, (int)other.Completion.Result!, "click outside cancels");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FormControls", "Tooltip", "Tooltip draws near the anchor and flips at the screen edge",
                        _ =>
                        {
                            Tooltip tip = new Tooltip();
                            CellBuffer screen = new CellBuffer(30, 6);
                            Rect box = tip.Render(new BufferSurface(screen), 28, 5, "Hello tip");
                            Check.True(box.X + box.Width <= 30 && box.Y + box.Height <= 6, "clamped inside");
                            Check.True(box.Y < 5, "flipped above the anchor");
                            Check.True(Snapshot.ToText(screen).Contains("Hello tip"), "text drawn");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FormControls", "HostTooltip", "The host shows a provider tooltip after the pointer rests",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 10);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                Button button = new Button("Go") { TooltipText = "UNIQUE_TIP" };
                                app.Layout = Layout.Create().Add("b", r => r.LeftAnchored(0, 20).FillHeight()).Build();
                                app.Bind("b", button);
                                app.TooltipDelayMilliseconds = 0;
                                app.Start();
                                backend.FeedInput("\u001b[<35;3;2M");
                                app.PumpInputOnce();
                                app.RenderOnce();
                                Check.True(backend.PeekOutput().Contains("UNIQUE_TIP"), "tooltip drawn");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FormControls", "Badge", "Badge renders padded text with variant colors",
                        _ =>
                        {
                            Badge badge = new Badge("Failed", BadgeVariant.Error);
                            Check.Equal(8, badge.Width, "text plus padding");
                            CellBuffer buffer = new CellBuffer(10, 1);
                            badge.Render(new BufferSurface(buffer));
                            Check.Equal("F", buffer.Get(1, 0).Grapheme, "padded");
                            Check.True(buffer.Get(1, 0).Style.Background == Color.FromPalette(1), "error background");
                            badge.ApplyTheme(TUIKit.Theming.Theme.Light);
                            Check.True(badge.ResolveStyle().Background == TUIKit.Theming.Theme.Light.Error.Foreground, "themed");
                            Check.Equal(" Failed ", badge.ToStyledText().ToPlainString(), "inline text");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FormControls", "FormHiddenRows", "Form hides rows from layout, focus, and validation",
                        _ =>
                        {
                            Form form = new Form();
                            TextField a = form.Add("A", new TextField());
                            TextField b = form.Add("B", new TextField(), () => "b invalid");
                            TextField c = form.Add("C", new TextField());
                            form.SetFieldVisible(1, false);
                            string text = Snapshot.RenderWidget(form, 20, 10);
                            Check.False(text.Contains("  B"), "hidden label not drawn");
                            form.HandleKey(KeyEvent.Special(KeyCode.Tab));
                            Check.Equal(2, form.FocusedIndex, "Tab skipped the hidden row");
                            Check.True(form.Validate() == null, "hidden row not validated");
                            form.SetFieldVisible(1, true);
                            Check.Equal("b invalid", form.Validate(), "visible again");
                            form.SetFieldEnabled(0, false);
                            form.HandleKey(KeyEvent.Special(KeyCode.Tab));
                            Check.Equal(1, form.FocusedIndex, "wrapped past the disabled first row");
                            Check.False(a.IsEnabled, "widget disabled");
                            Check.True(c != null && b != null, "fields exist");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FormControls", "FormDirty", "Form tracks dirty state from field change events",
                        _ =>
                        {
                            Form form = new Form();
                            TextField name = form.Add("Name", new TextField());
                            Checkbox flag = form.AddCheckbox("Enabled");
                            List<bool> states = new List<bool>();
                            form.DirtyChanged += d => states.Add(d);
                            Check.False(form.IsDirty, "clean at start");
                            name.Value = "x";
                            Check.True(form.IsDirty, "programmatic set marks dirty");
                            form.MarkClean();
                            flag.Checked = true;
                            Check.True(form.IsDirty, "checkbox marks dirty");
                            form.TrackChanges = false;
                            Check.False(form.IsDirty, "turning tracking off clears");
                            name.Value = "y";
                            Check.False(form.IsDirty, "not tracked");
                            Check.Equal("True,False,True,False", string.Join(",", states), "transitions");
                            string text = Snapshot.RenderWidget(form, 30, 8);
                            Check.True(text.Contains("[x] Enabled"), "checkbox field on one row");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FormControls", "FormNested", "A non-wrapping form bubbles Tab out after its last field",
                        _ =>
                        {
                            Form form = new Form { WrapFocus = false };
                            form.Add("A", new TextField());
                            form.Add("B", new TextField());
                            Check.True(form.HandleKey(KeyEvent.Special(KeyCode.Tab)), "moved to B");
                            Check.False(form.HandleKey(KeyEvent.Special(KeyCode.Tab)), "bubbled");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}

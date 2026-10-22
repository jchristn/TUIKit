namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Testing;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    /// <summary>Shared regression coverage for horizontal choices, confirmations, and connected activity logs.</summary>
    public static class PromptTimelineSuite
    {
        /// <summary>Builds deterministic input, layout, rendering, and validation tests.</summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor("PromptTimeline", "Prompts and Activity Timeline", new List<TestCaseDescriptor>
            {
                Case("VerticalCompatibility", () =>
                {
                    RadioGroup radio = new RadioGroup(new[] { "Yes", "No" });
                    WidgetTester tester = WidgetTester.For(radio, 25, 3).Render();
                    Check.Equal("(o) Yes", tester.Row(0), "Original selected marker");
                    Check.Equal("( ) No", tester.Row(1), "Original unselected marker");
                    Check.False(radio.HandleKey(KeyEvent.Special(KeyCode.Right)), "Vertical does not eat Right");
                    Check.True(radio.HandleKey(KeyEvent.Special(KeyCode.Down)), "Vertical Down");
                    Check.Equal(1, radio.SelectedIndex, "Second row");
                    Check.Equal(2, radio.Measure(new Size(25, 3)).Height, "Vertical height");
                }),
                Case("HorizontalKeysEvents", () =>
                {
                    RadioGroup radio = Horizontal();
                    int changes = 0;
                    int genericChanges = 0;
                    radio.SelectionChanged += (s, e) =>
                    {
                        Check.Equal(0, e.OldValue, "Old index");
                        Check.Equal(1, e.NewValue, "New index");
                        changes++;
                    };
                    radio.Changed += (s, e) => genericChanges++;
                    Check.False(radio.HandleKey(KeyEvent.Special(KeyCode.Down)), "Horizontal does not eat Down");
                    radio.HandleKey(KeyEvent.Special(KeyCode.Right));
                    radio.HandleKey(KeyEvent.Special(KeyCode.Right));
                    radio.HandleKey(KeyEvent.Special(KeyCode.End));
                    Check.Equal(1, changes, "One real selection change, clamped");
                    Check.Equal(1, genericChanges, "Companion event");
                    Check.Equal("1", radio.GetSnapshot(), "Form snapshot");
                    Check.Equal(1, radio.Measure(new Size(20, 4)).Height, "One row");
                }),
                Case("HorizontalRender", () =>
                {
                    RadioGroup radio = Horizontal();
                    WidgetTester tester = WidgetTester.For(radio, 30, 2).Render();
                    Check.Equal("● Yes / ○ No", tester.Row(0), "Inline symbols and separator");
                    Check.Equal("", tester.Row(1), "No second row");
                    tester.Press(KeyCode.Right).Render();
                    Check.Equal("○ Yes / ● No", tester.Row(0), "Selection moved");
                    tester.Press(KeyCode.Home).Press(KeyCode.End).Press(KeyCode.Left);
                    Check.Equal(0, radio.SelectedIndex, "Home End Left");
                }),
                Case("HorizontalMouseUnicodeWidth", () =>
                {
                    RadioGroup radio = new RadioGroup(new[] { "界", "No" })
                    {
                        Orientation = RadioGroupOrientation.Horizontal,
                        SelectedMarker = "●", UnselectedMarker = "○"
                    };
                    Check.False(radio.HandleMouse(Click(5, 0)), "Separator is not a choice");
                    Check.False(radio.HandleMouse(Click(7, 1)), "Other row ignored");
                    Check.False(radio.HandleMouse(Click(-1, 0)), "Negative column ignored");
                    Check.True(radio.HandleMouse(Click(7, 0)), "Second option after wide label");
                    Check.Equal(1, radio.SelectedIndex, "Clicked second option");
                    Check.False(radio.HandleMouse(Click(40, 0)), "Trailing space ignored");
                }),
                Case("HorizontalCustomMarkers", () =>
                {
                    RadioGroup radio = Horizontal();
                    radio.SelectedMarker = "[yes]";
                    radio.UnselectedMarker = "-";
                    radio.Separator = " | ";
                    Check.Equal("[yes] Yes | - No", WidgetTester.For(radio, 30, 1).Render().Row(0), "Different marker widths");
                    Check.True(radio.HandleMouse(Click(12, 0)), "Custom marker hit span");
                    Check.Equal(1, radio.SelectedIndex, "Selected custom choice");
                }),
                Case("RadioDisabledAndValidation", () =>
                {
                    RadioGroup radio = Horizontal();
                    radio.IsEnabled = false;
                    Check.False(radio.HandleKey(KeyEvent.Special(KeyCode.Right)), "Disabled keys");
                    Check.False(radio.HandleMouse(Click(8, 0)), "Disabled mouse");
                    Check.Throws<ArgumentOutOfRangeException>(() => radio.Orientation = (RadioGroupOrientation)9, "Invalid argument rejected");
                    Check.Throws<ArgumentNullException>(() => radio.SelectedMarker = null!, "Invalid argument rejected");
                    Check.Throws<ArgumentNullException>(() => radio.UnselectedMarker = null!, "Invalid argument rejected");
                    Check.Throws<ArgumentNullException>(() => radio.Separator = null!, "Invalid argument rejected");
                    Check.Throws<ArgumentException>(() => new RadioGroup(new[] { "Yes", null! }), "Invalid argument rejected");
                    string[] options = { "Yes", "No" };
                    RadioGroup copy = new RadioGroup(options);
                    options[0] = "changed";
                    Check.Equal("Yes", copy.SelectedOption, "Caller cannot mutate stored labels");
                }),
                Case("ConfirmSubmitNo", () =>
                {
                    ConfirmPrompt prompt = new ConfirmPrompt("Proceed?");
                    int completed = 0;
                    prompt.Completed += (s, e) =>
                    {
                        Check.True(prompt.IsCompleted, "State committed before callback");
                        completed++;
                    };
                    Check.False(prompt.SelectedValue, "Defaults to No");
                    Check.False(prompt.IsCompleted, "Initially pending");
                    Check.Equal<bool?>(null, prompt.Result, "No initial result");
                    Check.True(prompt.HandleKey(KeyEvent.Special(KeyCode.Enter)), "Submit consumed");
                    Check.Equal<bool?>(false, prompt.Result, "No is a submitted answer");
                    Check.False(prompt.HandleKey(KeyEvent.Special(KeyCode.Enter)), "No duplicate completion");
                    Check.False(prompt.HandleKey(KeyEvent.Special(KeyCode.Left)), "Completed selection frozen to input");
                    Check.Equal(1, completed, "One completion");
                    Check.Equal("│  No", WidgetTester.For(prompt, 30, 3).Render().Row(1), "Collapsed answer");
                    Check.Equal(0, prompt.GetKeyHints()!.Count, "No inactive hints");
                }),
                Case("ConfirmCancelResetYes", () =>
                {
                    ConfirmPrompt prompt = new ConfirmPrompt("Proceed?", true);
                    prompt.HandleKey(KeyEvent.Special(KeyCode.Escape));
                    Check.True(prompt.IsCompleted, "Cancellation is complete");
                    Check.Equal<bool?>(null, prompt.Result, "Cancel distinct from No");
                    Check.Equal("│  Cancelled", WidgetTester.For(prompt, 30, 3).Render().Row(1), "Cancelled text");
                    prompt.Reset(true);
                    Check.False(prompt.IsCompleted, "Reset pending");
                    Check.True(prompt.SelectedValue, "Reset choice");
                    Check.Equal(3, prompt.GetKeyHints()!.Count, "Active hints");
                    prompt.HandleKey(KeyEvent.Special(KeyCode.Enter));
                    Check.Equal<bool?>(true, prompt.Result, "Submitted Yes");
                }),
                Case("ConfirmRenderAndMouse", () =>
                {
                    ConfirmPrompt prompt = new ConfirmPrompt("Proceed with installation?", true);
                    Check.False(prompt.HandleMouse(Click(12, 1)), "No layout before first render");
                    WidgetTester tester = WidgetTester.For(prompt, 40, 4).Render();
                    Check.Equal("◆  Proceed with installation?", tester.Row(0), "Question gutter");
                    Check.Equal("│  ● Yes / ○ No", tester.Row(1), "Choices gutter");
                    Check.False(prompt.HandleMouse(Click(12, 0)), "Question not clickable");
                    Check.False(prompt.HandleMouse(Click(1, 1)), "Gutter not clickable");
                    Check.False(prompt.HandleMouse(Click(9, 1)), "Separator not clickable");
                    Check.True(prompt.HandleMouse(Click(12, 1)), "Translated choice hit");
                    Check.False(prompt.SelectedValue, "Clicked No");
                    Check.False(prompt.IsCompleted, "Click does not submit");
                }),
                Case("ConfirmWrapResizeClip", () =>
                {
                    ConfirmPrompt prompt = new ConfirmPrompt("one\r\ntwo", true);
                    Check.Equal(3, prompt.Measure(new Size(20, 10)).Height, "Explicit newline included");
                    WidgetTester tester = WidgetTester.For(prompt, 20, 4).Render();
                    Check.Equal("│  two", tester.Row(1), "Continuation gutter");
                    Check.Equal("│  ● Yes / ○ No", tester.Row(2), "Choices below wrapped question");
                    Check.True(prompt.HandleMouse(Click(12, 2)), "Wrapped hit row");
                    WidgetTester.For(prompt, 6, 1).Render();
                    Check.False(prompt.HandleMouse(Click(4, 2)), "Old choice row cleared by clipped render");
                    Check.Equal(1, prompt.Measure(new Size(6, 1)).Height, "Height constrained");
                    Check.Equal(Size.Empty, prompt.Measure(Size.Empty), "Zero size");
                }),
                Case("ConfirmAsciiAndTheme", () =>
                {
                    ConfirmPrompt prompt = new ConfirmPrompt("Proceed?", true) { UseAsciiGlyphs = true };
                    WidgetTester tester = WidgetTester.For(prompt, 35, 4).Render();
                    Check.Equal("*  Proceed?", tester.Row(0), "ASCII header");
                    Check.Equal("|  (o) Yes / ( ) No", tester.Row(1), "ASCII circles");
                    Check.True(prompt.HandleMouse(Click(15, 1)), "ASCII hit regions use marker widths");
                    Check.False(prompt.SelectedValue, "ASCII No selected");
                    prompt.ApplyTheme(Theme.Dark);
                    Check.Equal(Theme.Dark.Text, prompt.NormalStyle, "Theme text");
                    Check.Equal(Theme.Dark.UseAsciiBorders, prompt.UseAsciiGlyphs, "Theme glyph preference");
                    prompt.ApplyTheme(Theme.HighContrast);
                    Check.True(prompt.UseAsciiGlyphs, "High contrast ASCII");
                }),
                Case("ConfirmDisabledValidation", () =>
                {
                    ConfirmPrompt prompt = new ConfirmPrompt("Proceed?") { IsEnabled = false };
                    Check.False(prompt.HandleKey(KeyEvent.Special(KeyCode.Enter)), "Disabled submit");
                    WidgetTester.For(prompt, 30, 4).Render();
                    Check.False(prompt.HandleMouse(Click(4, 1)), "Disabled mouse");
                    Check.Equal(0, prompt.GetKeyHints()!.Count, "Disabled hints");
                    prompt.IsEnabled = true;
                    Check.False(prompt.HandleKey(KeyEvent.Special(KeyCode.Enter, KeyModifiers.Ctrl)), "Modified Enter ignored");
                    Check.Throws<ArgumentNullException>(() => new ConfirmPrompt(null!), "Invalid argument rejected");
                    Check.Throws<ArgumentNullException>(() => prompt.Question = null!, "Invalid argument rejected");
                    Check.Throws<ArgumentNullException>(() => prompt.HandleMouse(null!), "Invalid argument rejected");
                    Check.Throws<ArgumentNullException>(() => prompt.Render(null!), "Invalid argument rejected");
                    Check.Throws<ArgumentNullException>(() => prompt.ApplyTheme(null!), "Invalid argument rejected");
                }),
                Case("TimelineExample", () =>
                {
                    ActivityTimeline timeline = new ActivityTimeline();
                    timeline.Add("Repository cloned");
                    timeline.Add("Found 1 skill");
                    timeline.Add("Skill: humanizer", ActivityStatus.Active);
                    WidgetTester tester = WidgetTester.For(timeline, 30, 8).Render();
                    Check.Equal("◇  Repository cloned", tester.Row(0), "First diamond");
                    Check.Equal("│", tester.Row(1), "Connected spacer");
                    Check.Equal("◇  Found 1 skill", tester.Row(2), "Second diamond");
                    Check.Equal("│", tester.Row(3), "Second connector");
                    Check.Equal("●  Skill: humanizer", tester.Row(4), "Active dot");
                    Check.Equal("", tester.Row(5), "No trailing connector");
                    Check.Equal(5, timeline.Measure(new Size(30, 20)).Height, "Measured height");
                }),
                Case("TimelineStatusesAndUpdates", () =>
                {
                    ActivityTimeline timeline = new ActivityTimeline { Spacing = 0 };
                    timeline.Add("Pending", ActivityStatus.Pending);
                    timeline.Add("Active", ActivityStatus.Active);
                    timeline.Add("Done");
                    ActivityEntry failed = timeline.Add("Failed", ActivityStatus.Failed);
                    WidgetTester tester = WidgetTester.For(timeline, 30, 8).Render();
                    Check.Equal("○  Pending", tester.Row(0), "Pending marker");
                    Check.Equal("●  Active", tester.Row(1), "Active marker");
                    Check.Equal("◇  Done", tester.Row(2), "Completed marker");
                    Check.Equal("×  Failed", tester.Row(3), "Failure marker");
                    failed.Status = ActivityStatus.Completed;
                    failed.Text = "Retried";
                    failed.Detail = "Success";
                    tester.Render();
                    Check.Equal("◇  Retried", tester.Row(3), "Live status and text");
                    Check.Equal("   Success", tester.Row(4), "Last detail without dangling connector");
                    Check.Equal(4, timeline.Entries.Count, "Read-only entry view");
                }),
                Case("TimelineAscii", () =>
                {
                    ActivityTimeline timeline = new ActivityTimeline { Spacing = 0, UseAsciiGlyphs = true };
                    timeline.Add("Pending", ActivityStatus.Pending);
                    timeline.Add("Active", ActivityStatus.Active);
                    timeline.Add("Done");
                    timeline.Add("Failed", ActivityStatus.Failed);
                    WidgetTester tester = WidgetTester.For(timeline, 20, 4).Render();
                    Check.Equal("o  Pending", tester.Row(0), "ASCII pending");
                    Check.Equal("*  Active", tester.Row(1), "ASCII active");
                    Check.Equal("+  Done", tester.Row(2), "ASCII done");
                    Check.Equal("x  Failed", tester.Row(3), "ASCII failed");
                }),
                Case("TimelineWrappingAndDetails", () =>
                {
                    ActivityTimeline timeline = new ActivityTimeline();
                    timeline.Add("abcdefgh", detail: "detail\nnext");
                    timeline.Add("tail");
                    WidgetTester tester = WidgetTester.For(timeline, 7, 10).Render();
                    Check.Equal("◇  abcd", tester.Row(0), "Wrap at content width");
                    Check.Equal("│  efgh", tester.Row(1), "Wrapped primary connector");
                    Check.Equal("│  deta", tester.Row(2), "Wrapped detail");
                    Check.Equal("│  il", tester.Row(3), "Remaining detail");
                    Check.Equal("│  next", tester.Row(4), "Explicit newline");
                    Check.Equal("│", tester.Row(5), "Spacer");
                    Check.Equal("◇  tail", tester.Row(6), "Next entry");
                    Check.Equal(7, timeline.Measure(new Size(7, 20)).Height, "Measure agrees with render");
                    Check.Equal(5, timeline.Measure(new Size(20, 20)).Height, "Reflow at wider width");
                }),
                Case("TimelineWideAndCombining", () =>
                {
                    ActivityTimeline timeline = new ActivityTimeline();
                    timeline.Add("界界e\u0301");
                    WidgetTester tester = WidgetTester.For(timeline, 7, 4).Render();
                    Check.True(tester.Row(0).Contains("界界"), "Wide glyphs fit four cells");
                    Check.Equal("   e\u0301", tester.Row(1), "Combining cluster preserved on next row");
                    Check.Equal(2, timeline.Measure(new Size(7, 10)).Height, "Unicode column measure");
                }),
                Case("TimelineNarrowAndClear", () =>
                {
                    ActivityTimeline timeline = new ActivityTimeline();
                    timeline.Add("界a");
                    timeline.Add("b");
                    WidgetTester tester = WidgetTester.For(timeline, 4, 6).Render();
                    Check.Equal("◇", tester.Row(0), "Too-wide cluster clipped intact");
                    Check.Equal("│  a", tester.Row(1), "One-cell continuation");
                    Check.Equal(4, timeline.Measure(new Size(4, 10)).Height, "Narrow measure");
                    WidgetTester.For(timeline, 1, 1).Render();
                    Check.Equal(1, timeline.Measure(new Size(1, 1)).Height, "Narrow clipped height");
                    Check.Equal(Size.Empty, timeline.Measure(Size.Empty), "Zero measure");
                    timeline.Clear();
                    tester.Render();
                    Check.Equal("", tester.Row(0), "Clear removes old pixels");
                    Check.Equal(0, timeline.Entries.Count, "Empty entries");
                    Check.Equal(0, timeline.Measure(new Size(10, 10)).Height, "Empty height");
                }),
                Case("TimelineThemeBackground", () =>
                {
                    ActivityTimeline timeline = new ActivityTimeline();
                    timeline.ApplyTheme(Theme.HighContrast);
                    Check.True(timeline.UseAsciiGlyphs, "Theme fallback");
                    Check.Equal(Theme.HighContrast.Accent, timeline.ActiveStyle, "Accent");
                    Check.Equal(Theme.HighContrast.Success, timeline.CompletedStyle, "Success");
                    Check.Equal(Theme.HighContrast.Error, timeline.FailedStyle, "Error");
                    timeline.NormalStyle = CellStyle.Default.WithBackground(Color.FromPalette(4));
                    timeline.ActiveStyle = CellStyle.Default.WithForeground(Color.FromPalette(6));
                    timeline.Add("Hello", ActivityStatus.Active);
                    CellBuffer buffer = new CellBuffer(20, 4);
                    timeline.Render(new BufferSurface(buffer));
                    Check.Equal(Color.FromPalette(4), buffer.Get(19, 3).Style.Background, "Background fills surface");
                    Check.Equal(Color.FromPalette(4), buffer.Get(0, 0).Style.Background, "Marker inherits background");
                }),
                Case("TimelineValidation", () =>
                {
                    ActivityTimeline timeline = new ActivityTimeline();
                    Check.Throws<ArgumentNullException>(() => timeline.Add(null!), "Invalid argument rejected");
                    Check.Throws<ArgumentOutOfRangeException>(() => timeline.Add("bad", (ActivityStatus)8), "Invalid argument rejected");
                    Check.Equal(0, timeline.Entries.Count, "Invalid Add leaves timeline unchanged");
                    Check.Throws<ArgumentOutOfRangeException>(() => timeline.Spacing = -1, "Invalid argument rejected");
                    ActivityEntry entry = timeline.Add("valid");
                    Check.Throws<ArgumentNullException>(() => entry.Text = null!, "Invalid argument rejected");
                    Check.Throws<ArgumentOutOfRangeException>(() => entry.Status = (ActivityStatus)(-1), "Invalid argument rejected");
                    Check.Throws<ArgumentNullException>(() => timeline.ApplyTheme(null!), "Invalid argument rejected");
                    Check.Throws<ArgumentNullException>(() => timeline.Render(null!), "Invalid argument rejected");
                    timeline.Spacing = int.MaxValue;
                    timeline.Add("second");
                    Check.Equal(4, timeline.Measure(new Size(20, 4)).Height, "Spacing cannot overflow measure");
                    WidgetTester.For(timeline, 20, 4).Render();
                })
            });
        }

        private static TestCaseDescriptor Case(string id, Action test)
        {
            return new TestCaseDescriptor("PromptTimeline", id, id, _ =>
            {
                test();
                return Task.CompletedTask;
            });
        }

        private static RadioGroup Horizontal()
        {
            return new RadioGroup(new[] { "Yes", "No" })
            {
                Orientation = RadioGroupOrientation.Horizontal,
                SelectedMarker = "●", UnselectedMarker = "○"
            };
        }

        private static MouseEvent Click(int x, int y)
        {
            return new MouseEvent(MouseEventKind.Press, MouseButton.Left, x, y, KeyModifiers.None, 1);
        }
    }
}

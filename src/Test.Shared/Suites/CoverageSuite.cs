namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Diagnostics;
    using TUIKit.Input;
    using TUIKit.Layout;
    using TUIKit.Modals;
    using TUIKit.Terminal;
    using TUIKit.Widgets;

    /// <summary>
    /// Touchstone suite that exercises the remaining public surface — value-type formatting and
    /// equality, ANSI sequence builders, styled-text helpers, editor movement, and widget rendering —
    /// to broaden coverage of the library's API.
    /// </summary>
    public static class CoverageSuite
    {
        /// <summary>
        /// Builds the coverage suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Coverage",
                displayName: "API Surface Coverage",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Coverage", "ValueFormatting", "Value types format and compare",
                        _ =>
                        {
                            Check.Equal("(1, 2)", new Point(1, 2).ToString(), "Point");
                            Check.Equal("3x4", new Size(3, 4).ToString(), "Size");
                            Check.Equal("(1, 2, 3x4)", new Rect(1, 2, 3, 4).ToString(), "Rect");
                            Check.Equal("#FF8800", Color.FromRgb(0xFF, 0x88, 0x00).ToString(), "Rgb");
                            Check.Equal("palette:5", Color.FromPalette(5).ToString(), "Palette");
                            Check.Equal("default", Color.Default.ToString(), "Default color");
                            Check.True(new Point(0, 0) != new Point(1, 1), "Point inequality");
                            Check.True(new Size(2, 2) != new Size(3, 3), "Size inequality");
                            Check.True(CellStyle.Default != CellStyle.Default.WithAttribute(CellAttributes.Bold, true), "Style inequality");
                            Check.Equal(Point.Origin, new Rect(0, 0, 5, 5).Location, "Location");

                            CellStyle merged = CellStyle.Default
                                .WithForeground(Color.FromPalette(1))
                                .Over(CellStyle.Default.WithBackground(Color.FromRgb(10, 20, 30)));
                            Check.Equal(Color.FromPalette(1), merged.Foreground, "Over keeps explicit foreground");
                            Check.Equal(Color.FromRgb(10, 20, 30), merged.Background, "Over inherits background");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Coverage", "AnsiSequences", "ANSI builders produce non-empty sequences",
                        _ =>
                        {
                            Check.True(Ansi.EnterAltScreen.Length > 0, "Alt screen");
                            Check.True(Ansi.ExitAltScreen.Length > 0, "Exit alt");
                            Check.True(Ansi.HideCursor.Length > 0, "Hide");
                            Check.True(Ansi.ShowCursor.Length > 0, "Show");
                            Check.True(Ansi.ClearScreen.Length > 0, "Clear");
                            Check.True(Ansi.EnableMouse.Length > 0 && Ansi.DisableMouse.Length > 0, "Mouse");
                            Check.True(Ansi.EnableBracketedPaste.Length > 0 && Ansi.DisableBracketedPaste.Length > 0, "Paste");
                            Check.True(Ansi.PushKittyKeyboard.Length > 0 && Ansi.PopKittyKeyboard.Length > 0, "Kitty");
                            Check.True(Ansi.OpenHyperlink("1", "http://x").Contains("http://x"), "Hyperlink");
                            Check.True(Ansi.CloseHyperlink.Length > 0, "Close link");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Coverage", "StyledHelpers", "Styled-text helpers apply attributes and colors",
                        _ =>
                        {
                            StyledText t = Text.From("x").Dim().Italic().Underline().Strikethrough().Reverse();
                            CellStyle style = t.Spans[0].Style;
                            Check.True(style.HasAttribute(CellAttributes.Dim), "Dim");
                            Check.True(style.HasAttribute(CellAttributes.Italic), "Italic");
                            Check.True(style.HasAttribute(CellAttributes.Underline), "Underline");
                            Check.True(style.HasAttribute(CellAttributes.Strikethrough), "Strike");
                            Check.True(style.HasAttribute(CellAttributes.Reverse), "Reverse");

                            Check.Equal(Color.FromPalette(5), Text.From("m").Magenta().Spans[0].Style.Foreground, "Magenta");
                            Check.Equal(Color.FromPalette(3), Text.From("y").Yellow().Spans[0].Style.Foreground, "Yellow");
                            Check.Equal(0, Text.Empty.Width, "Empty width");
                            Check.Equal("ab", Text.From("a").Append("b").ToPlainString(), "Append string");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Coverage", "EditorMovement", "Editor movement and forward delete",
                        _ =>
                        {
                            TextEditor editor = new TextEditor();
                            editor.Text = "ab\ncd";
                            editor.MoveUp();
                            editor.MoveDown();
                            editor.MoveLeft();
                            editor.MoveRight();
                            editor.HandleKey(KeyEvent.Special(KeyCode.Home));
                            editor.HandleKey(KeyEvent.Special(KeyCode.End));
                            editor.HandleKey(KeyEvent.Special(KeyCode.Up));
                            editor.HandleKey(KeyEvent.Special(KeyCode.Down));
                            editor.DeleteForward();
                            Check.True(editor.Text.Length >= 3, "Still has content");

                            editor.IsFocused = true;
                            CellBuffer buffer = new CellBuffer(6, 3);
                            editor.Render(new BufferSurface(buffer));
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Coverage", "WidgetRenders", "Assorted widgets render without error",
                        _ =>
                        {
                            CellBuffer buffer = new CellBuffer(20, 4);
                            BufferSurface surface = new BufferSurface(buffer);

                            new Label(Text.From("label")).Render(surface);
                            ProgressBar bar = new ProgressBar();
                            bar.Value = 0.5;
                            bar.Render(surface);
                            Spinner spinner = new Spinner();
                            string frame = spinner.CurrentFrame;
                            spinner.Advance();
                            Check.False(spinner.CurrentFrame == frame, "Spinner advanced");
                            spinner.Render(surface);

                            RadioGroup radio = new RadioGroup(new[] { "a", "b" });
                            radio.HandleKey(KeyEvent.Special(KeyCode.Down));
                            Check.Equal(1, radio.SelectedIndex, "Radio moved");
                            radio.Render(surface);

                            Checkbox box = new Checkbox("c", true);
                            box.Render(surface);

                            TextField field = new TextField();
                            field.Value = "hi";
                            field.IsFocused = true;
                            field.Render(surface);
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Coverage", "PaneAndKeyChord", "Pane controls and chord formatting",
                        _ =>
                        {
                            TUIKit.Content.Pane pane = new TUIKit.Content.Pane("p");
                            pane.ScrollbackCapacityLines = 100;
                            pane.MaxLineLength = 500;
                            pane.WriteLine("a");
                            Check.Equal(1, pane.SnapshotPlainLines().Count, "Snapshot");
                            Check.True(pane.Version > 0, "Version advanced");
                            using (pane.BeginBatch())
                            {
                                pane.WriteLine("b");
                                pane.WriteLine("c");
                            }

                            pane.Clear();
                            Check.Equal(0, pane.LineCount, "Cleared");

                            Check.Equal("ctrl+shift+a", KeyChord.Parse("ctrl+shift+a").ToString(), "Chord round-trip");
                            Check.Equal("f5", KeyChord.Parse("f5").ToString(), "Function chord");
                            Check.Equal("A", KeyEvent.Char('A').AsText(), "Key text");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Coverage", "OverlaysAndNotify", "Debug overlay and notification render",
                        _ =>
                        {
                            Layout layout = Layout.Create().Add("a", r => r.FillWidth().FillHeight()).Build();
                            CellBuffer buffer = new CellBuffer(30, 8);
                            BufferSurface surface = new BufferSurface(buffer);
                            FrameStats stats = new FrameStats();
                            stats.Record(12);
                            DebugOverlay.Render(surface, layout, stats);

                            NotificationCenter center = new NotificationCenter();
                            center.MaxConcurrent = 3;
                            center.DefaultTimeoutMilliseconds = 1000;
                            center.Add("info", NotificationSeverity.Info, 0);
                            center.Add("err", NotificationSeverity.Error, 0);
                            center.Render(surface, 10);
                            Check.Equal(2, center.Active(10).Count, "Two active");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}

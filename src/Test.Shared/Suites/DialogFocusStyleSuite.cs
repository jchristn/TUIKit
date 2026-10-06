namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Hosting;
    using TUIKit.Modals;
    using TUIKit.Terminal;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for dialog focus colors (1.5.0, B8): <see cref="DialogModal.FocusedBorderStyle"/>,
    /// <see cref="DialogModal.FocusedTitleStyle"/>, <see cref="DialogModal.UseThemeFocusStyles"/>, and
    /// <see cref="DialogModal.ApplyFocusTheme"/>.
    /// </summary>
    public static class DialogFocusStyleSuite
    {
        /// <summary>
        /// Builds the dialog focus style suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "DialogFocusStyle",
                displayName: "Dialog Focused Border Color and Title Style",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("DialogFocusStyle", "ThemeFocusWhileTopmost", "With UseThemeFocusStyles on, the topmost dialog's border uses the focus style and loses it beneath another modal",
                        _ =>
                        {
                            Theme theme = Theme.Dark;
                            CellStyle focus = FocusFrame.FocusedStyle(theme);
                            ModalStack stack = new ModalStack();
                            ProbeDialogModal first = new ProbeDialogModal(20, 3) { Title = "First", UseThemeFocusStyles = true };
                            first.ApplyFocusTheme(theme);
                            stack.Push(first);

                            CellBuffer buffer = Draw(first);
                            Check.Equal(focus, buffer.Get(first.FrameBounds.X, first.FrameBounds.Y).Style, "topmost corner in the focus style");
                            Check.Equal(theme.Resolve(Theme.FocusTitleRole, focus), TitleCell(buffer, first).Style, "title in the focus title style");

                            stack.Push(new ProbeDialogModal(10, 2));
                            buffer = Draw(first);
                            Check.Equal(first.BorderStyleColor, buffer.Get(first.FrameBounds.X, first.FrameBounds.Y).Style, "beneath another modal: plain border style");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("DialogFocusStyle", "ExplicitStylesWin", "Explicit focused styles override the theme",
                        _ =>
                        {
                            CellStyle border = CellStyle.Default.WithForeground(Color.FromPalette(5));
                            CellStyle title = CellStyle.Default.WithForeground(Color.FromPalette(2)).WithAttribute(CellAttributes.Underline, true);
                            ModalStack stack = new ModalStack();
                            ProbeDialogModal dialog = new ProbeDialogModal(20, 3) { Title = "T", UseThemeFocusStyles = true, FocusedBorderStyle = border, FocusedTitleStyle = title };
                            dialog.ApplyFocusTheme(Theme.Dark);
                            stack.Push(dialog);
                            CellBuffer buffer = Draw(dialog);
                            Check.Equal(border, buffer.Get(dialog.FrameBounds.X, dialog.FrameBounds.Y).Style, "explicit border style");
                            Check.Equal(title, TitleCell(buffer, dialog).Style, "explicit title style");

                            ProbeDialogModal noTheme = new ProbeDialogModal(20, 3) { UseThemeFocusStyles = true };
                            stack.Push(noTheme);
                            buffer = Draw(noTheme);
                            Check.Equal(noTheme.BorderStyleColor.WithAttribute(CellAttributes.Bold, true), buffer.Get(noTheme.FrameBounds.X, noTheme.FrameBounds.Y).Style, "no theme applied yet: bold border color");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("DialogFocusStyle", "OffKeeps14", "With UseThemeFocusStyles off, the dialog renders exactly as in 1.4.0",
                        _ =>
                        {
                            ModalStack stack = new ModalStack();
                            ProbeDialogModal dialog = new ProbeDialogModal(20, 3) { Title = "Same", FocusedBorder = BorderStyle.Double };
                            dialog.ApplyFocusTheme(Theme.Dark);
                            stack.Push(dialog);
                            CellBuffer actual = Draw(dialog);

                            CellBuffer expected = new CellBuffer(60, 20);
                            BufferSurface surface = new BufferSurface(expected);
                            Rect box = dialog.FrameBounds;
                            surface.Fill(box, Cell.Blank(dialog.BackgroundStyle));
                            surface.DrawBox(box, dialog.BorderStyleColor, BorderStyle.Double, "Same");
                            for (int y = box.Top; y < box.Bottom; y++)
                            {
                                Check.Equal(expected.Get(box.Left, y), actual.Get(box.Left, y), "left edge row " + y);
                                Check.Equal(expected.Get(box.Right - 1, y), actual.Get(box.Right - 1, y), "right edge row " + y);
                            }

                            for (int x = box.Left; x < box.Right; x++)
                                Check.Equal(expected.Get(x, box.Top), actual.Get(x, box.Top), "top edge column " + x);
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("DialogFocusStyle", "HostAppliesTheme", "The host hands its theme to a dialog it shows",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(60, 16);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                app.Theme = Theme.Light;
                                app.Start();
                                ProbeDialogModal dialog = new ProbeDialogModal(20, 3) { UseThemeFocusStyles = true };
                                Task<object?> shown = app.ShowAsync(dialog);
                                Check.False(shown.IsCompleted, "dialog open");
                                app.RenderOnce();
                                CellBuffer frame = app.CaptureFrame()!;
                                Check.Equal(FocusFrame.FocusedStyle(Theme.Light), frame.Get(dialog.FrameBounds.X, dialog.FrameBounds.Y).Style, "light theme focus style");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("DialogFocusStyle", "Guards", "ApplyFocusTheme rejects a null theme",
                        _ =>
                        {
                            ProbeDialogModal dialog = new ProbeDialogModal(10, 2);
                            Check.Throws<ArgumentNullException>(() => dialog.ApplyFocusTheme(null!), "null theme");
                            Check.False(dialog.UseThemeFocusStyles, "off by default");
                            Check.True(dialog.FocusedBorderStyle == null && dialog.FocusedTitleStyle == null, "styles null by default");
                            return Task.CompletedTask;
                        })
                });
        }

        private static CellBuffer Draw(DialogModal dialog)
        {
            CellBuffer buffer = new CellBuffer(60, 20);
            dialog.Render(new BufferSurface(buffer));
            return buffer;
        }

        private static Cell TitleCell(CellBuffer buffer, DialogModal dialog)
        {
            Rect box = dialog.FrameBounds;
            for (int x = box.Left + 1; x < box.Right - 1; x++)
            {
                Cell cell = buffer.Get(x, box.Top);
                if (cell.Grapheme.Length == 1 && char.IsLetter(cell.Grapheme[0]))
                    return cell;
            }

            throw new InvalidOperationException("No title cell found on the dialog's top edge.");
        }
    }
}

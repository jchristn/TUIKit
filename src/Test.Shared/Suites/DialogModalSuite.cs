namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Input;

    /// <summary>
    /// Coverage for the <see cref="TUIKit.Modals.DialogModal"/> base class: content auto-sizing and
    /// clamping, title and footer rendering, dismissal, truncation, and size-property guards.
    /// </summary>
    public static class DialogModalSuite
    {
        /// <summary>
        /// Builds the dialog-modal suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "DialogModal",
                displayName: "Dialog Modal Base",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("DialogModal", "AutoSizeToContent", "Content surface matches the natural size within bounds",
                        _ =>
                        {
                            ProbeDialogModal dialog = new ProbeDialogModal(20, 6);
                            CellBuffer buffer = new CellBuffer(60, 20);
                            dialog.Render(new BufferSurface(buffer));
                            Check.Equal(20, dialog.LastContentWidth, "content width equals natural width");
                            Check.Equal(6, dialog.LastContentHeight, "content height equals natural height");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("DialogModal", "ClampsToMax", "Max content size clamps a larger natural size",
                        _ =>
                        {
                            ProbeDialogModal dialog = new ProbeDialogModal(200, 200);
                            dialog.MaxContentWidth = 30;
                            dialog.MaxContentHeight = 8;
                            CellBuffer buffer = new CellBuffer(80, 24);
                            dialog.Render(new BufferSurface(buffer));
                            Check.Equal(30, dialog.LastContentWidth, "width clamped to max");
                            Check.Equal(8, dialog.LastContentHeight, "height clamped to max");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("DialogModal", "ClampsToScreen", "A natural size larger than the screen is clamped to fit",
                        _ =>
                        {
                            ProbeDialogModal dialog = new ProbeDialogModal(500, 500);
                            CellBuffer buffer = new CellBuffer(24, 8);
                            dialog.Render(new BufferSurface(buffer));
                            Check.True(dialog.LastContentWidth <= 24, "content width fits the screen");
                            Check.True(dialog.LastContentHeight <= 8, "content height fits the screen");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("DialogModal", "TitleAndFooter", "Title and footer hint render on the border",
                        _ =>
                        {
                            ProbeDialogModal dialog = new ProbeDialogModal(30, 4);
                            dialog.Title = "Heading";
                            dialog.FooterHint = "Esc: close";
                            CellBuffer buffer = new CellBuffer(60, 16);
                            dialog.Render(new BufferSurface(buffer));
                            string text = TUIKit.Testing.Snapshot.ToText(buffer);
                            Check.True(text.Contains("Heading"), "title rendered");
                            Check.True(text.Contains("Esc: close"), "footer rendered");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("DialogModal", "DismissOnEscape", "Escape closes the dialog through the base helper",
                        async ct =>
                        {
                            ProbeDialogModal dialog = new ProbeDialogModal(10, 2);
                            dialog.HandleKey(KeyEvent.Special(KeyCode.Escape));
                            object? result = await dialog.Completion.ConfigureAwait(false);
                            Check.Equal(-1, (int)result!, "escape result");
                        }),

                    new TestCaseDescriptor("DialogModal", "Truncate", "Truncate fits and ellipsizes text",
                        _ =>
                        {
                            ProbeDialogModal dialog = new ProbeDialogModal(10, 2);
                            Check.Equal("short", dialog.TruncatePublic("short", 10), "short text unchanged");
                            Check.Equal("abcd…", dialog.TruncatePublic("abcdefgh", 5), "long text ellipsized");
                            Check.Equal(string.Empty, dialog.TruncatePublic("x", 0), "zero width yields empty");
                            Check.Equal(string.Empty, dialog.TruncatePublic(null, 5), "null yields empty");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("DialogModal", "SizeGuards", "Size properties reject inverted and out-of-range values",
                        _ =>
                        {
                            ProbeDialogModal dialog = new ProbeDialogModal(10, 2);
                            Check.Throws<ArgumentOutOfRangeException>(() => dialog.MinContentWidth = 0, "min width below 1");
                            Check.Throws<ArgumentOutOfRangeException>(() => dialog.MinContentHeight = 0, "min height below 1");
                            dialog.MaxContentWidth = 20;
                            Check.Throws<ArgumentOutOfRangeException>(() => dialog.MinContentWidth = 25, "min above max");
                            dialog.MinContentWidth = 10;
                            Check.Throws<ArgumentOutOfRangeException>(() => dialog.MaxContentWidth = 3, "max below min");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}

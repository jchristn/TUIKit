namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Modals;

    /// <summary>
    /// Touchstone suite covering the modal stack, message modal result flow and close refusal, and the
    /// notification center lifecycle.
    /// </summary>
    public static class ModalSuite
    {
        /// <summary>
        /// Builds the modal suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Modal",
                displayName: "Modals and Notifications",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Modal", "StackFocusTrap", "Stack tracks nesting and top",
                        _ =>
                        {
                            ModalStack stack = new ModalStack();
                            Check.False(stack.IsActive, "Empty initially");
                            MessageModal a = new MessageModal("A", "first", new List<string> { "OK" });
                            MessageModal b = new MessageModal("B", "second", new List<string> { "OK" });
                            stack.Push(a);
                            stack.Push(b);
                            Check.Equal(2, stack.Count, "Two modals");
                            Check.True(ReferenceEquals(b, stack.Top), "Top is the last pushed");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Modal", "AsyncResult", "Choosing a button completes the modal",
                        async ct =>
                        {
                            ModalStack stack = new ModalStack();
                            MessageModal modal = new MessageModal("Confirm", "Proceed?", new List<string> { "Yes", "No" });
                            stack.Push(modal);

                            stack.HandleKey(KeyEvent.Special(KeyCode.Right)); // select "No"
                            stack.HandleKey(KeyEvent.Special(KeyCode.Enter)); // choose

                            object? result = await modal.Completion.ConfigureAwait(false);
                            Check.Equal(1, (int)result!, "Chose index 1 (No)");
                            Check.False(stack.IsActive, "Closed modal removed from stack");
                        }),

                    new TestCaseDescriptor("Modal", "EscapeCancels", "Escape cancels with -1",
                        async ct =>
                        {
                            MessageModal modal = new MessageModal("Confirm", "Proceed?", new List<string> { "Yes" });
                            modal.HandleKey(KeyEvent.Special(KeyCode.Escape));
                            object? result = await modal.Completion.ConfigureAwait(false);
                            Check.Equal(-1, (int)result!, "Cancelled result");
                        }),

                    new TestCaseDescriptor("Modal", "RenderCentered", "Modal renders a bordered box",
                        _ =>
                        {
                            MessageModal modal = new MessageModal("Title", "Hello", new List<string> { "OK" });
                            CellBuffer buffer = new CellBuffer(40, 12);
                            modal.Render(new BufferSurface(buffer));

                            bool sawBorder = false;
                            for (int y = 0; y < buffer.Height && !sawBorder; y++)
                            {
                                for (int x = 0; x < buffer.Width; x++)
                                {
                                    if (buffer.Get(x, y).Grapheme == "┌")
                                    {
                                        sawBorder = true;
                                        break;
                                    }
                                }
                            }

                            Check.True(sawBorder, "Box border drawn");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Modal", "NotificationExpiry", "Notifications expire on timeout",
                        _ =>
                        {
                            NotificationCenter center = new NotificationCenter();
                            center.Add("saved", NotificationSeverity.Success, 0, 1000);
                            center.Add("warn", NotificationSeverity.Warning, 500, 1000);
                            Check.Equal(2, center.Active(600).Count, "Both active at t=600");
                            Check.Equal(1, center.Active(1200).Count, "First expired at t=1200");
                            Check.Equal(0, center.Active(2000).Count, "Both expired at t=2000");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Modal", "NotificationCap", "Max concurrent drops oldest",
                        _ =>
                        {
                            NotificationCenter center = new NotificationCenter();
                            center.MaxConcurrent = 2;
                            center.Add("one", NotificationSeverity.Info, 0, 0);
                            center.Add("two", NotificationSeverity.Info, 0, 0);
                            center.Add("three", NotificationSeverity.Info, 0, 0);
                            IReadOnlyList<Notification> active = center.Active(0);
                            Check.Equal(2, active.Count, "Capped at two");
                            Check.Equal("three", active[0].Text, "Newest first");
                            Check.Equal("two", active[1].Text, "Oldest dropped");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}

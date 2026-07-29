namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Modals;
    using TUIKit.Terminal;

    /// <summary>
    /// Validation coverage for the terminal backends, capability detection, modal types, the modal
    /// stack, and the notification center.
    /// </summary>
    public static class BackendModalValidationSuite
    {
        /// <summary>
        /// Builds the backend/modal validation suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "BackendModalValidation",
                displayName: "Backend & Modal Validation",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("BackendModalValidation", "ConsoleBackend", "ConsoleBackend reports a size and guards its I/O",
                        _ =>
                        {
                            using (ConsoleBackend backend = new ConsoleBackend())
                            {
                                Check.True(backend.Size.Width >= 1 && backend.Size.Height >= 1, "reports a usable size (falls back when not a TTY)");
                                Check.True(backend.Capabilities != null, "capabilities detected");
                                Check.Throws<ArgumentNullException>(() => backend.Write(null!), "write null");
                                Check.Throws<ArgumentNullException>(() => backend.ReadInput(null!, 0, 1), "read into null");
                                Check.Throws<ArgumentOutOfRangeException>(() => backend.ReadInput(new byte[4], -1, 1), "read negative offset");
                                Check.Throws<ArgumentOutOfRangeException>(() => backend.ReadInput(new byte[4], 0, 5), "read count over capacity");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("BackendModalValidation", "HeadlessAndCapabilities", "Headless backend bounds and capability detection validate arguments",
                        _ =>
                        {
                            Check.Throws<ArgumentOutOfRangeException>(() => new HeadlessBackend(0, 24), "zero width backend");
                            Check.Throws<ArgumentOutOfRangeException>(() => new HeadlessBackend(80, 0), "zero height backend");
                            HeadlessBackend backend = new HeadlessBackend(20, 5);
                            Check.Throws<ArgumentOutOfRangeException>(() => backend.Resize(-1, 5), "resize negative width");

                            Check.Throws<ArgumentNullException>(() => CapabilityDetector.Detect(null!, true), "null env accessor");
                            TerminalCapabilities caps = CapabilityDetector.Detect(name => null, true);
                            Check.True(caps != null, "detection returns capabilities");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("BackendModalValidation", "Modals", "Modal types validate their construction arguments",
                        _ =>
                        {
                            List<string> buttons = new List<string> { "OK" };
                            Check.Throws<ArgumentNullException>(() => new MessageModal(null!, "m", buttons), "null message-modal title");
                            Check.Throws<ArgumentNullException>(() => new MessageModal("t", null!, buttons), "null message-modal message");
                            Check.Throws<ArgumentNullException>(() => new MessageModal("t", "m", null!), "null buttons");
                            Check.Throws<ArgumentException>(() => new MessageModal("t", "m", new List<string>()), "empty buttons");

                            Check.Throws<ArgumentNullException>(() => new PromptModal(null!), "null prompt title");
                            Check.Throws<ArgumentNullException>(() => new SelectModal(null!, new List<string> { "a" }), "null select title");
                            Check.Throws<ArgumentNullException>(() => new SelectModal("t", null!), "null select options");
                            Check.Throws<ArgumentException>(() => new SelectModal("t", new List<string>()), "empty select options");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("BackendModalValidation", "StackAndNotifications", "Modal stack and notifications validate arguments and bounds",
                        _ =>
                        {
                            ModalStack stack = new ModalStack();
                            Check.Throws<ArgumentNullException>(() => stack.Push(null!), "push null modal");
                            Check.Throws<ArgumentNullException>(() => stack.Render(null!), "render null surface");

                            NotificationCenter center = new NotificationCenter();
                            Check.Throws<ArgumentOutOfRangeException>(() => center.MaxConcurrent = 0, "non-positive max concurrent");
                            Check.Throws<ArgumentOutOfRangeException>(() => center.DefaultTimeoutMilliseconds = -1, "negative default timeout");
                            Check.Throws<ArgumentNullException>(() => center.Add(null!, NotificationSeverity.Info, 0, 1000), "null notification text");
                            Check.Throws<ArgumentNullException>(() => center.Render(null!, 0), "null notification surface");

                            Check.Throws<ArgumentNullException>(() => new Notification(null!, NotificationSeverity.Info, 0, 1000), "null notification ctor text");
                            Check.Throws<ArgumentOutOfRangeException>(() => new Notification("x", NotificationSeverity.Info, 0, -1), "negative notification timeout");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}

namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Modals;
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for 1.9 item C7: <see cref="WizardModal"/> and <see cref="WizardOutcome"/>.
    /// </summary>
    public static class WizardModalSuite
    {
        private const string _Id = "WizardModal";

        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: _Id,
                displayName: "Wizard Modal",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(_Id, "Finished", "Finishing the wizard completes the modal with Finished",
                        async _ =>
                        {
                            Wizard wizard = WizardSuite.Three(out TextField name, out TextField _, out TextField _);
                            WizardModal modal = new WizardModal("Setup", wizard);
                            name.Value = "ada";
                            wizard.Next();
                            wizard.Next();
                            wizard.Next();
                            object? result = await modal.Completion.ConfigureAwait(false);
                            Check.Equal(WizardOutcome.Finished, (WizardOutcome)result!, "Finished");
                            Check.True(ReferenceEquals(wizard, modal.Wizard), "exposed");
                        }),

                    new TestCaseDescriptor(_Id, "EscCancels", "Esc cancels the wizard and completes with Cancelled; other keys go to the wizard",
                        async _ =>
                        {
                            Wizard wizard = WizardSuite.Three(out TextField name, out TextField _, out TextField _);
                            WizardModal modal = new WizardModal("Setup", wizard);
                            Check.True(modal.HandleKey(KeyEvent.Char('a')), "typed");
                            Check.Equal("a", name.Value, "into the step field");
                            Check.True(modal.HandlePaste("da"), "paste");
                            Check.Equal("ada", name.Value, "pasted into the step field");
                            int cancelled = 0;
                            wizard.Cancelled += (s, e) => cancelled++;
                            modal.HandleKey(KeyEvent.Special(KeyCode.Escape));
                            Check.Equal(1, cancelled, "Esc called Cancel");
                            object? result = await modal.Completion.ConfigureAwait(false);
                            Check.Equal(WizardOutcome.Cancelled, (WizardOutcome)result!, "Cancelled");
                        }),

                    new TestCaseDescriptor(_Id, "Guards", "Constructor and size guards",
                        _ =>
                        {
                            Wizard wizard = WizardSuite.Three(out TextField _, out TextField _, out TextField _);
                            Check.Throws<ArgumentNullException>(() => new WizardModal(null!, wizard), "null title");
                            Check.Throws<ArgumentNullException>(() => new WizardModal("x", null!), "null wizard");
                            WizardModal modal = new WizardModal("x", wizard);
                            Check.Throws<ArgumentOutOfRangeException>(() => modal.NaturalWidth = 5, "width");
                            Check.Throws<ArgumentOutOfRangeException>(() => modal.NaturalHeight = 2, "height");
                            Check.Throws<ArgumentNullException>(() => modal.ApplyTheme(null!), "null theme");
                            BufferSurface surface = new BufferSurface(new CellBuffer(80, 24));
                            modal.Render(surface);
                            Check.True(modal.ContentBounds.Width == 64, "default width");
                            return Task.CompletedTask;
                        }),
                });
        }
    }
}

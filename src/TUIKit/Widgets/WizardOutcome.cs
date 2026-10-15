namespace TUIKit.Widgets
{
    /// <summary>
    /// How a <see cref="Modals.WizardModal"/> closed: its <see cref="Modals.Modal.Completion"/> result.
    /// </summary>
    public enum WizardOutcome
    {
        /// <summary>The user finished the last step.</summary>
        Finished = 0,

        /// <summary>The user cancelled (the Cancel button, Esc, or <see cref="Wizard.Cancel"/>).</summary>
        Cancelled = 1
    }
}

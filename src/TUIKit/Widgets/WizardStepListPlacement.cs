namespace TUIKit.Widgets
{
    /// <summary>
    /// Where a <see cref="Wizard"/> draws its step list. See <see cref="Wizard.StepListPlacement"/>.
    /// </summary>
    public enum WizardStepListPlacement
    {
        /// <summary>One row under the header. The default.</summary>
        Top = 0,

        /// <summary>A column on the left of the step content.</summary>
        Left = 1,

        /// <summary>No step list; the header still names the step.</summary>
        None = 2
    }
}

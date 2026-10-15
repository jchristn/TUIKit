namespace TUIKit.Widgets
{
    using System;

    /// <summary>
    /// Event data for <see cref="Wizard.StepChanging"/>, raised before every move between steps. Set
    /// <see cref="Cancel"/> to veto the move, for example to keep the user on a step whose data could not be saved.
    /// </summary>
    public sealed class WizardStepChangingEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the index (into <see cref="Wizard.Steps"/>) of the step being left.
        /// </summary>
        public int From { get; }

        /// <summary>
        /// Gets the index (into <see cref="Wizard.Steps"/>) of the step being entered.
        /// </summary>
        public int To { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the move is vetoed. Defaults to false.
        /// </summary>
        public bool Cancel { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="WizardStepChangingEventArgs"/> class.
        /// </summary>
        /// <param name="from">The step being left.</param>
        /// <param name="to">The step being entered.</param>
        public WizardStepChangingEventArgs(int from, int to)
        {
            From = from;
            To = to;
        }
    }
}

namespace TUIKit.Widgets
{
    using System;

    /// <summary>
    /// Event data for <see cref="Wizard.StepChanged"/>, raised after a move between steps. Instances are immutable.
    /// </summary>
    public sealed class WizardStepChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the index (into <see cref="Wizard.Steps"/>) of the step that was left.
        /// </summary>
        public int From { get; }

        /// <summary>
        /// Gets the index (into <see cref="Wizard.Steps"/>) of the step now current.
        /// </summary>
        public int To { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="WizardStepChangedEventArgs"/> class.
        /// </summary>
        /// <param name="from">The step that was left.</param>
        /// <param name="to">The step now current.</param>
        public WizardStepChangedEventArgs(int from, int to)
        {
            From = from;
            To = to;
        }
    }
}

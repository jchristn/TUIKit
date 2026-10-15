namespace TUIKit.Widgets
{
    using System;

    /// <summary>
    /// One step of a <see cref="Wizard"/>: a title, the widget shown for the step, and its rules for moving on.
    /// </summary>
    /// <remarks>Not thread-safe: configure steps on the UI thread.</remarks>
    public sealed class WizardStep
    {
        /// <summary>
        /// Gets the step title, shown in the header and the step list. Never null.
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// Gets the widget shown while the step is current. Never null. A <see cref="Form"/> content gets
        /// <see cref="Form.WrapFocus"/> set to false so Tab moves on to the wizard's buttons.
        /// </summary>
        public IWidget Content { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the step can be skipped: a Skip button is shown and
        /// <see cref="Wizard.Skip"/> moves on without validating. Defaults to false.
        /// </summary>
        public bool Optional { get; set; }

        /// <summary>
        /// Gets or sets the step validator: it returns an error message, or null to allow leaving forward. Null (the
        /// default) means the step is always valid. It runs on <see cref="Wizard.Next"/> and on forward
        /// <see cref="Wizard.GoTo"/> jumps, never on <see cref="Wizard.Back"/>.
        /// </summary>
        public Func<string?>? Validate { get; set; }

        /// <summary>
        /// Gets or sets a predicate deciding whether the step applies, for flows that branch on an earlier answer.
        /// Null (the default) means always. A step whose predicate is false is skipped in both directions and left
        /// out of the step list and the count.
        /// </summary>
        public Func<bool>? IsApplicable { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="WizardStep"/> class.
        /// </summary>
        /// <param name="title">The step title. Must not be null.</param>
        /// <param name="content">The step widget. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public WizardStep(string title, IWidget content)
        {
            Title = title ?? throw new ArgumentNullException(nameof(title));
            Content = content ?? throw new ArgumentNullException(nameof(content));
        }

        internal bool Applies()
        {
            Func<bool>? predicate = IsApplicable;
            return predicate == null || predicate();
        }
    }
}

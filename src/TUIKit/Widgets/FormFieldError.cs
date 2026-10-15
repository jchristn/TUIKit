namespace TUIKit.Widgets
{
    using System;

    /// <summary>
    /// One validation error from <see cref="Form.ValidateAll"/>. Instances are immutable and thread-safe.
    /// </summary>
    public sealed class FormFieldError
    {
        /// <summary>
        /// Gets the zero-based index of the field with the error.
        /// </summary>
        public int FieldIndex { get; }

        /// <summary>
        /// Gets the field's label. Never null; empty for an inline field such as a checkbox.
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// Gets the error message. Never null or empty.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FormFieldError"/> class.
        /// </summary>
        /// <param name="fieldIndex">The field index. At least 0.</param>
        /// <param name="label">The field label. Must not be null.</param>
        /// <param name="message">The message. Must not be null or empty.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="fieldIndex"/> is negative.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="label"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="message"/> is null or empty.</exception>
        public FormFieldError(int fieldIndex, string label, string message)
        {
            if (fieldIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(fieldIndex), "The field index must be zero or greater.");
            if (string.IsNullOrEmpty(message))
                throw new ArgumentException("An error message must not be null or empty.", nameof(message));

            FieldIndex = fieldIndex;
            Label = label ?? throw new ArgumentNullException(nameof(label));
            Message = message;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return Label.Length > 0 ? Label + ": " + Message : Message;
        }
    }
}

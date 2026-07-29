namespace TUIKit.Widgets
{
    using System;

    /// <summary>
    /// A labeled field within a <see cref="Form"/>: a caption, the interactive widget, and an
    /// optional validator that returns an error message or null when the value is acceptable.
    /// </summary>
    internal sealed class FormField
    {
        internal string Label { get; }

        internal IWidget Widget { get; }

        internal IFocusable Focusable { get; }

        internal Func<string?>? Validator { get; }

        internal FormField(string label, IWidget widget, IFocusable focusable, Func<string?>? validator)
        {
            Label = label;
            Widget = widget;
            Focusable = focusable;
            Validator = validator;
        }
    }
}

namespace TUIKit.Widgets
{
    using System;

    /// <summary>
    /// A single labeled field in a <see cref="Form"/>: its label, its input widget, the focusable view of
    /// that widget, an optional validator, and its visibility and layout flags.
    /// </summary>
    internal sealed class FormField
    {
        internal string Label { get; set; }

        internal IWidget Widget { get; }

        internal IFocusable Focusable { get; }

        internal Func<string?>? Validator { get; }

        internal bool Visible { get; set; } = true;

        internal bool Inline { get; set; }

        internal FormField(string label, IWidget widget, IFocusable focusable, Func<string?>? validator)
        {
            Label = label;
            Widget = widget;
            Focusable = focusable;
            Validator = validator;
        }
    }
}

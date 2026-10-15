namespace TUIKit.Widgets
{
    /// <summary>
    /// A section heading in a <see cref="Form"/>, drawn before the field at <see cref="BeforeField"/> (or after
    /// the last field when it equals the field count). Sections take no focus and do not shift field indexes.
    /// </summary>
    internal sealed class FormSection
    {
        internal int BeforeField { get; }

        internal string Title { get; }

        internal FormSection(int beforeField, string title)
        {
            BeforeField = beforeField;
            Title = title;
        }
    }
}

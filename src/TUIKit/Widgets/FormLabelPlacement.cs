namespace TUIKit.Widgets
{
    /// <summary>
    /// Where a <see cref="Form"/> draws field labels. See <see cref="Form.LabelPlacement"/>.
    /// </summary>
    public enum FormLabelPlacement
    {
        /// <summary>On a row above the field, the 1.7 layout. The default.</summary>
        Above = 0,

        /// <summary>To the left of the field, aligned to <see cref="Form.LabelWidth"/>; single-row fields sit on the label's row.</summary>
        Left = 1
    }
}

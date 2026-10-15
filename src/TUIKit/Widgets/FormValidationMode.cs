namespace TUIKit.Widgets
{
    /// <summary>
    /// When a <see cref="Form"/> validates fields on its own. See <see cref="Form.ValidationMode"/>.
    /// </summary>
    public enum FormValidationMode
    {
        /// <summary>Errors appear after a submit attempt (<see cref="Form.Submit"/>), and a field's error is re-checked when it changes. The default.</summary>
        OnSubmit = 0,

        /// <summary>A field is validated when focus leaves it.</summary>
        OnLeave = 1,

        /// <summary>A field is validated on every change.</summary>
        OnChange = 2
    }
}

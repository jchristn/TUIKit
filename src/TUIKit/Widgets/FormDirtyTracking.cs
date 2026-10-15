namespace TUIKit.Widgets
{
    /// <summary>
    /// How a <see cref="Form"/> decides it has unsaved changes. See <see cref="Form.DirtyTracking"/>.
    /// </summary>
    public enum FormDirtyTracking
    {
        /// <summary>Any change event marks the form dirty until <see cref="Form.MarkClean"/>, the 1.7 behavior. The default.</summary>
        Events = 0,

        /// <summary>The form is dirty exactly while some field's value differs from its snapshot at <see cref="Form.MarkClean"/>, so a revert clears it.</summary>
        Snapshot = 1
    }
}

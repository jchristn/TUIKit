namespace TUIKit.Widgets
{
    using System;

    /// <summary>
    /// Implemented by widgets that hold a user-editable value or selection and announce every change to
    /// it, whether from input or from a programmatic set. It is the untyped companion of each widget's
    /// typed change event (for example <see cref="TextField.ValueChanged"/>), used by containers such as
    /// <see cref="Form"/> to track dirty state without knowing the widget type.
    /// </summary>
    public interface IChangeNotifier
    {
        /// <summary>
        /// Raised after the widget's value or selection changed. Never raised when a set leaves the value
        /// unchanged.
        /// </summary>
        event EventHandler? Changed;
    }
}

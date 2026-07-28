namespace TUIKit.Modals
{
    /// <summary>
    /// The severity of a transient notification, which drives its color.
    /// </summary>
    public enum NotificationSeverity
    {
        /// <summary>Informational.</summary>
        Info = 0,

        /// <summary>A successful outcome.</summary>
        Success = 1,

        /// <summary>A warning.</summary>
        Warning = 2,

        /// <summary>An error.</summary>
        Error = 3
    }
}

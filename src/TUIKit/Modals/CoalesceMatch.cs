namespace TUIKit.Modals
{
    /// <summary>
    /// How <see cref="NotificationCenter"/> decides that a new raise repeats a toast still on screen (see
    /// <see cref="NotificationCenter.CoalesceBy"/>). A raise with a coalesce key
    /// (<see cref="NotificationOptions.CoalesceKey"/>) always matches by key alone.
    /// </summary>
    public enum CoalesceMatch
    {
        /// <summary>
        /// Same severity, text, and title, and actions that match one for one: by
        /// <see cref="NotificationAction.Key"/> when both have one, otherwise by label. Actions built fresh
        /// on every call (a new lambda each time) still coalesce, and the newest callbacks replace the old
        /// ones. The default since 1.5.0.
        /// </summary>
        Content = 0,

        /// <summary>
        /// Same severity, text, and title, and the very same <see cref="NotificationAction"/> instances. The
        /// 1.4.0 behavior; callers that build actions per call never coalesce.
        /// </summary>
        ContentAndActionInstances = 1
    }
}

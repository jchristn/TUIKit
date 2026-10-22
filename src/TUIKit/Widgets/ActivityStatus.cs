namespace TUIKit.Widgets
{
    /// <summary>The state of an activity timeline entry; symbols remain distinct without color.</summary>
    public enum ActivityStatus
    {
        /// <summary>Waiting to start: ○, or o in ASCII.</summary>
        Pending = 0,
        /// <summary>Currently running: ●, or * in ASCII.</summary>
        Active = 1,
        /// <summary>Finished or informational: ◇, or + in ASCII.</summary>
        Completed = 2,
        /// <summary>Failed: ×, or x in ASCII.</summary>
        Failed = 3
    }
}

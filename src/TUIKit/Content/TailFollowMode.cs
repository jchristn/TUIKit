namespace TUIKit.Content
{
    /// <summary>
    /// How a scrollable view follows content appended at its end (see <see cref="TailFollow"/>).
    /// </summary>
    public enum TailFollowMode
    {
        /// <summary>
        /// Follow new content while the viewport is at the bottom; stop when the user moves the viewport
        /// away from the bottom and resume when it returns there. Selection changes alone never stop
        /// following. The default.
        /// </summary>
        FollowAtBottom = 0,

        /// <summary>
        /// Always show the newest content: appends return the viewport to the bottom even after the user
        /// scrolls up. Suits a live log that must never fall behind.
        /// </summary>
        AlwaysFollow = 1,

        /// <summary>
        /// Never move the viewport for appended content; new rows are only counted.
        /// <see cref="TailFollow.ReturnToTail"/> still jumps to the bottom once.
        /// </summary>
        Never = 2
    }
}

namespace TUIKit.Modals
{
    using System.Collections.Generic;
    using TUIKit;

    /// <summary>
    /// The screen geometry of one rendered toast, recorded by <see cref="NotificationCenter.Render"/> so
    /// a later click can be mapped to the toast, one of its action labels, or its dismiss marker.
    /// </summary>
    internal sealed class ToastHitRegion
    {
        internal Notification Notification { get; }

        internal Rect Bounds { get; }

        internal List<Rect> ActionRects { get; } = new List<Rect>();

        internal Rect DismissRect { get; set; }

        internal ToastHitRegion(Notification notification, Rect bounds)
        {
            Notification = notification;
            Bounds = bounds;
        }
    }
}

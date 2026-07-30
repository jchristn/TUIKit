namespace TUIKit.Hosting
{
    using TUIKit;
    using TUIKit.Widgets;

    /// <summary>
    /// One entry in the host's per-frame mouse hit-test map: the rectangle a bound widget occupied on
    /// the last frame, captured during the draw pass so the host can route a mouse event to the widget
    /// under the cursor. The map is host-owned and rebuilt every frame, so it stays correct when the
    /// same widget instance is bound twice or rendered headlessly at multiple sizes.
    /// </summary>
    internal sealed class HitTestEntry
    {
        internal HitTestEntry(string regionId, IWidget widget, Rect rect)
        {
            RegionId = regionId;
            Widget = widget;
            Rect = rect;
        }

        internal string RegionId { get; }

        internal IWidget Widget { get; }

        internal Rect Rect { get; }
    }
}

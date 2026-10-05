namespace TUIKit.Testing
{
    /// <summary>
    /// The kinds of focus defect <see cref="FocusAudit"/> reports.
    /// </summary>
    public enum FocusAuditProblemKind
    {
        /// <summary>
        /// Focusable regions exist but no focusable widget holds focus at this stop (nothing is focused,
        /// or the focused region's widget was replaced by one that cannot take focus).
        /// </summary>
        NoFocusedLeaf = 0,

        /// <summary>
        /// Focus is invisible at this stop: the focused region draws no focus frame (no border with
        /// <c>TuiApplication.HighlightFocusedRegion</c> or <c>RegionBuilder.WithFocusedBorder</c>), and no
        /// container on the path shows focus itself (a <c>SplitView</c> with pane frames, or a
        /// <c>TabView</c> whose strip holds focus).
        /// </summary>
        NoFocusIndicator = 1,

        /// <summary>
        /// Pressing Tab left focus exactly where it was, so the user cannot move past this stop.
        /// </summary>
        TraversalStuck = 2,

        /// <summary>
        /// Tab never brought focus back to where it started within the stop limit.
        /// </summary>
        TraversalDidNotCycle = 3,

        /// <summary>
        /// Shift+Tab does not visit the stops in the reverse order of Tab.
        /// </summary>
        TraversalNotSymmetric = 4,

        /// <summary>
        /// The region layout (the rectangles widgets render into) changed when focus moved.
        /// </summary>
        LayoutShifted = 5,

        /// <summary>
        /// Focus landed on something the user cannot see: a widget on the path reports
        /// <c>IHideable.IsVisible</c> false (hidden, or empty with nothing to show), or the focused region is
        /// not in the current layout or has no room on screen.
        /// </summary>
        InvisibleStop = 6
    }
}

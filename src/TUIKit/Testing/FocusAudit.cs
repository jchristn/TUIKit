namespace TUIKit.Testing
{
    using System;
    using System.Collections.Generic;
    using TUIKit.Hosting;
    using TUIKit.Input;
    using TUIKit.Layout;
    using TUIKit.Widgets;

    /// <summary>
    /// Sweeps an application's keyboard focus the way a user would, pressing Tab through every stop and
    /// Shift+Tab back, and reports focus defects: a stop where nothing holds focus, a stop on a hidden or
    /// empty widget, a stop where focus is invisible, a widget that traps Tab, a ring that never comes back to the start, Shift+Tab that does
    /// not retrace Tab, and a layout that shifts when focus moves. Run it from a test over each screen of
    /// an application to keep focus usable as the interface grows.
    /// </summary>
    /// <remarks>
    /// The audit drives the real input path (key routing, focus-scoped commands, containers), so it sees
    /// what a user sees, and it reads typed state (<see cref="TuiApplication.CurrentFocusPath"/>, the
    /// region layout) rather than guessing from rendered colors. The application must be started on an
    /// interactive backend (for example <see cref="Terminal.HeadlessBackend"/>) with no modal open. Focus
    /// is back at its starting stop when the audit finishes cleanly. Run it on the UI thread, with the
    /// run loop not running.
    /// </remarks>
    public static class FocusAudit
    {
        /// <summary>
        /// Runs the audit.
        /// </summary>
        /// <param name="app">The started application. Must not be null.</param>
        /// <param name="options">The options, or null for the defaults.</param>
        /// <returns>The result. Never null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="app"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the application has not been started on
        /// an interactive backend, or a modal is open.</exception>
        public static FocusAuditResult Run(TuiApplication app, FocusAuditOptions? options = null)
        {
            if (app == null)
                throw new ArgumentNullException(nameof(app));

            FocusAuditOptions settings = options ?? new FocusAuditOptions();
            app.RenderOnce();
            if (app.CaptureFrame() == null)
                throw new InvalidOperationException("The focus audit needs an application started on an interactive backend.");
            if (app.Modals.IsActive)
                throw new InvalidOperationException("Close open modals before running the focus audit; they capture Tab.");

            List<FocusAuditProblem> problems = new List<FocusAuditProblem>();
            List<FocusPath> stops = new List<FocusPath>();
            FocusPath start = app.CurrentFocusPath;
            string layout = app.HitMapSignature();
            bool cycled = false;
            bool stuck = false;

            FocusPath current = start;
            for (int press = 0; press < settings.MaxStops; press++)
            {
                stops.Add(current);
                CheckStop(app, settings, stops.Count - 1, current, layout, problems);

                Press(app, KeyEvent.Special(KeyCode.Tab));
                FocusPath next = app.CurrentFocusPath;
                bool loneStop = stops.Count == 1 && app.FocusOrder.Count <= 1;
                if (next.Equals(current) && !loneStop)
                {
                    problems.Add(new FocusAuditProblem(stops.Count - 1, FocusAuditProblemKind.TraversalStuck, current, "Tab did not move focus off this stop."));
                    stuck = true;
                    break;
                }

                if (next.Equals(start))
                {
                    cycled = true;
                    break;
                }

                current = next;
            }

            if (!cycled && !stuck)
                problems.Add(new FocusAuditProblem(stops.Count - 1, FocusAuditProblemKind.TraversalDidNotCycle, current, "Tab did not return to the starting stop within " + settings.MaxStops + " presses."));

            if (cycled && settings.CheckSymmetry && stops.Count > 1)
            {
                for (int i = stops.Count - 1; i >= 0; i--)
                {
                    Press(app, KeyEvent.Special(KeyCode.Tab, KeyModifiers.Shift));
                    FocusPath back = app.CurrentFocusPath;
                    if (!back.Equals(stops[i]))
                    {
                        problems.Add(new FocusAuditProblem(i, FocusAuditProblemKind.TraversalNotSymmetric, back, "Shift+Tab reached " + back + " where Tab had visited " + stops[i] + "."));
                        break;
                    }
                }
            }

            return new FocusAuditResult(stops.Count, problems);
        }

        private static void Press(TuiApplication app, KeyEvent key)
        {
            app.InjectInput(InputEvent.FromKey(key));
            app.RenderOnce();
        }

        private static void CheckStop(TuiApplication app, FocusAuditOptions options, int stop, FocusPath path, string layout, List<FocusAuditProblem> problems)
        {
            if (path.Leaf == null)
            {
                if (app.FocusOrder.Count > 0)
                    problems.Add(new FocusAuditProblem(stop, FocusAuditProblemKind.NoFocusedLeaf, path, "Focusable regions exist but no focusable widget holds focus."));
                return;
            }

            if (path.Region != null && !app.IsRegionShown(path.Region))
            {
                problems.Add(new FocusAuditProblem(stop, FocusAuditProblemKind.InvisibleStop, path, "Region '" + path.Region + "' is not on screen."));
                return;
            }

            for (int i = 0; i < path.Nodes.Count; i++)
            {
                if (path.Nodes[i] is IHideable hideable && !hideable.IsVisible)
                {
                    problems.Add(new FocusAuditProblem(stop, FocusAuditProblemKind.InvisibleStop, path, path.Nodes[i].GetType().Name + " is hidden or has nothing to show."));
                    return;
                }
            }

            if (options.CheckFocusIndicator && !ShowsFocus(app, path))
                problems.Add(new FocusAuditProblem(stop, FocusAuditProblemKind.NoFocusIndicator, path, "Region '" + path.Region + "' draws no focus frame and no container on the path shows focus."));

            if (options.CheckLayoutStable && !string.Equals(app.HitMapSignature(), layout, StringComparison.Ordinal))
                problems.Add(new FocusAuditProblem(stop, FocusAuditProblemKind.LayoutShifted, path, "The region layout differs from the first stop."));
        }

        private static bool ShowsFocus(TuiApplication app, FocusPath path)
        {
            Layout? layout = app.Layout;
            if (layout != null)
            {
                IReadOnlyList<Region> regions = layout.Regions;
                for (int i = 0; i < regions.Count; i++)
                {
                    Region region = regions[i];
                    if (string.Equals(region.Id, path.Region, StringComparison.Ordinal)
                        && region.HasBorder
                        && (app.HighlightFocusedRegion || region.FocusedBorder.HasValue))
                        return true;
                }
            }

            for (int i = 0; i < path.Nodes.Count; i++)
            {
                object node = path.Nodes[i];
                if (node is SplitView split && split.ShowPaneFrames && split.ForwardKeys)
                    return true;
                if (node is TabView tabs && tabs.IsStripFocused)
                    return true;
            }

            return false;
        }
    }
}

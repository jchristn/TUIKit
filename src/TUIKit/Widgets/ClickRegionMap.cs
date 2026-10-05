namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Diagnostics;
    using TUIKit.Input;

    /// <summary>
    /// The clickable areas a custom-rendered widget drew this frame (buttons inside list rows, inline
    /// links, chips), and the mouse handling that turns a click on one into its action. The host routes
    /// a click to a widget; this map finds which part of the widget was clicked.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rule that keeps clicks correct when content scrolls: call <see cref="Clear"/> at the start of
    /// every render and record each area as it is drawn, in the same coordinates the widget draws in.
    /// The widget's <see cref="IMouseAware.HandleMouse"/> receives events in those same coordinates (the
    /// host and scrolling containers translate them), so passing them to <see cref="HandleMouse"/> needs
    /// no offset arithmetic, and an area that scrolled out of view was simply not recorded.
    /// </para>
    /// <para>
    /// <see cref="InlineButton.Draw"/> records areas for you and uses the pointer position tracked here
    /// for hover styling. Not thread-safe: render and handle mouse on the UI thread.
    /// </para>
    /// </remarks>
    /// <typeparam name="TAction">The application's action type, for example an enum or a record.</typeparam>
    public sealed class ClickRegionMap<TAction>
    {
        private readonly List<ClickRegion<TAction>> _Regions = new List<ClickRegion<TAction>>();
        private int _PointerX = -1;
        private int _PointerY = -1;

        /// <summary>
        /// Raised when a left click lands on a recorded area, with that area. Raised from
        /// <see cref="HandleMouse"/> on the UI thread.
        /// </summary>
        public event Action<ClickRegion<TAction>>? Invoked;

        /// <summary>
        /// Gets the areas recorded since the last <see cref="Clear"/>, in drawing order. Never null.
        /// </summary>
        public IReadOnlyList<ClickRegion<TAction>> Regions
        {
            get { return _Regions; }
        }

        /// <summary>
        /// Gets the number of recorded areas.
        /// </summary>
        public int Count
        {
            get { return _Regions.Count; }
        }

        /// <summary>
        /// Gets the last pointer column seen by <see cref="HandleMouse"/>, or -1 when the pointer is not
        /// over the widget.
        /// </summary>
        public int PointerX
        {
            get { return _PointerX; }
        }

        /// <summary>
        /// Gets the last pointer row seen by <see cref="HandleMouse"/>, or -1 when the pointer is not over
        /// the widget.
        /// </summary>
        public int PointerY
        {
            get { return _PointerY; }
        }

        /// <summary>
        /// Removes every recorded area. Call it at the start of each render so no stale area from the
        /// previous frame can be clicked.
        /// </summary>
        public void Clear()
        {
            _Regions.Clear();
        }

        /// <summary>
        /// Records a clickable area. An empty rectangle is ignored and returns null.
        /// </summary>
        /// <param name="area">The rectangle, in the widget's drawing coordinates.</param>
        /// <param name="action">The action.</param>
        /// <param name="key">The equivalent key, or null.</param>
        /// <param name="tooltip">The tooltip text, or null.</param>
        /// <returns>The recorded region, or null when <paramref name="area"/> is empty.</returns>
        public ClickRegion<TAction>? Add(Rect area, TAction action, string? key = null, string? tooltip = null)
        {
            if (area.IsEmpty)
                return null;

            ClickRegion<TAction> region = new ClickRegion<TAction>(area, action, key, tooltip);
            _Regions.Add(region);
            return region;
        }

        /// <summary>
        /// Finds the area under a point. When areas overlap, the one recorded last (drawn on top) wins.
        /// </summary>
        /// <param name="x">The column, in the widget's drawing coordinates.</param>
        /// <param name="y">The row, in the widget's drawing coordinates.</param>
        /// <returns>The area, or null when the point is not over one.</returns>
        public ClickRegion<TAction>? HitTest(int x, int y)
        {
            Point point = new Point(x, y);
            for (int i = _Regions.Count - 1; i >= 0; i--)
            {
                if (_Regions[i].Area.Contains(point))
                    return _Regions[i];
            }

            return null;
        }

        /// <summary>
        /// Determines whether the pointer is currently over a rectangle, for hover styling while drawing.
        /// </summary>
        /// <param name="area">The rectangle, in the widget's drawing coordinates.</param>
        /// <returns><c>true</c> when the last known pointer position lies inside it.</returns>
        public bool IsHovered(Rect area)
        {
            return _PointerX >= 0 && area.Contains(new Point(_PointerX, _PointerY));
        }

        /// <summary>
        /// Handles a mouse event in the widget's own coordinates: tracks the pointer for hover, and on a
        /// left press over a recorded area raises <see cref="Invoked"/>. Call it from the widget's
        /// <see cref="IMouseAware.HandleMouse"/>.
        /// </summary>
        /// <param name="mouse">The event, in widget-local coordinates. Must not be null.</param>
        /// <returns><c>true</c> when the event invoked an area; otherwise <c>false</c>, so the widget can
        /// handle it (for example, select the row under a click between buttons).</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mouse"/> is null.</exception>
        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            switch (mouse.Kind)
            {
                case MouseEventKind.Leave:
                    _PointerX = -1;
                    _PointerY = -1;
                    return false;
                case MouseEventKind.Enter:
                case MouseEventKind.Move:
                    _PointerX = mouse.X;
                    _PointerY = mouse.Y;
                    return false;
                case MouseEventKind.Press:
                    _PointerX = mouse.X;
                    _PointerY = mouse.Y;
                    if (mouse.Button != MouseButton.Left)
                        return false;

                    ClickRegion<TAction>? hit = HitTest(mouse.X, mouse.Y);
                    if (hit == null)
                        return false;

                    TuiKitInstruments.Add(TuiKitInstruments.ClickRegionsInvoked, 1);
                    Invoked?.Invoke(hit);
                    return true;
                default:
                    return false;
            }
        }
    }
}

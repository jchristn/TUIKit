namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Widgets;

    /// <summary>
    /// A test widget that records every mouse event routed to it, with configurable consumption, so
    /// host routing and hover-synthesis tests can assert on delivery order and coordinates.
    /// </summary>
    public sealed class RecordingMouseWidget : IWidget, IMouseAware
    {
        private readonly List<MouseEvent> _Events = new List<MouseEvent>();

        /// <summary>
        /// Gets the mouse events received, in delivery order. Never null.
        /// </summary>
        public IReadOnlyList<MouseEvent> Events
        {
            get { return _Events; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether <see cref="HandleMouse"/> reports Press, Release,
        /// Move, and Wheel events as consumed. Enter and Leave are always reported unconsumed.
        /// Defaults to false.
        /// </summary>
        public bool ConsumeEvents { get; set; }

        /// <summary>
        /// Records the event and reports consumption per <see cref="ConsumeEvents"/>.
        /// </summary>
        /// <param name="mouse">The mouse event. Must not be null.</param>
        /// <returns>The configured consumption result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mouse"/> is null.</exception>
        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            _Events.Add(mouse);
            if (mouse.Kind == MouseEventKind.Enter || mouse.Kind == MouseEventKind.Leave)
                return false;

            return ConsumeEvents;
        }

        /// <summary>
        /// Clears the recorded events.
        /// </summary>
        public void Clear()
        {
            _Events.Clear();
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            return available;
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));
        }
    }
}

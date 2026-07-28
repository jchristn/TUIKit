namespace TUIKit.Input
{
    using System;
    using System.Collections.Generic;
    using TUIKit;

    /// <summary>
    /// Maps screen regions to <see cref="Link"/> instances for the current frame. A link may occupy
    /// several hit rectangles (when it wraps across lines). The registry is rebuilt each frame from
    /// the visible viewport, so links scrolled out of view are simply absent. Hit-testing is by
    /// absolute cell coordinate.
    /// </summary>
    /// <remarks>Not thread-safe; built and queried on the render/input thread.</remarks>
    public sealed class LinkRegistry
    {
        private readonly List<Link> _Links = new List<Link>();
        private readonly List<Rect> _Rects = new List<Rect>();
        private readonly List<int> _RectLink = new List<int>();
        private int _NextId = 1;

        /// <summary>
        /// Gets the links added to this registry. Never null.
        /// </summary>
        public IReadOnlyList<Link> Links
        {
            get { return _Links; }
        }

        /// <summary>
        /// Removes all links and hit rectangles so the registry can be rebuilt for a new frame.
        /// </summary>
        public void Clear()
        {
            _Links.Clear();
            _Rects.Clear();
            _RectLink.Clear();
            _NextId = 1;
        }

        /// <summary>
        /// Adds a link occupying a single hit rectangle and assigns it an identifier.
        /// </summary>
        /// <param name="text">The display text. Must not be null.</param>
        /// <param name="hitRect">The rectangle that activates the link.</param>
        /// <param name="uri">The target URI, or null.</param>
        /// <param name="commandId">The command identifier, or null.</param>
        /// <param name="payload">An opaque payload, or null.</param>
        /// <returns>The created link.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public Link Add(string text, Rect hitRect, string? uri = null, string? commandId = null, object? payload = null)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            Link link = new Link(_NextId++, text, uri, commandId, payload);
            _Links.Add(link);
            _Rects.Add(hitRect);
            _RectLink.Add(_Links.Count - 1);
            return link;
        }

        /// <summary>
        /// Adds an additional hit rectangle for a link already in the registry, used when a link wraps
        /// across lines.
        /// </summary>
        /// <param name="link">The link. Must not be null and must belong to this registry.</param>
        /// <param name="hitRect">The additional rectangle.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="link"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the link is not in this registry.</exception>
        public void AddRect(Link link, Rect hitRect)
        {
            if (link == null)
                throw new ArgumentNullException(nameof(link));

            int index = _Links.IndexOf(link);
            if (index < 0)
                throw new ArgumentException("The link does not belong to this registry.", nameof(link));

            _Rects.Add(hitRect);
            _RectLink.Add(index);
        }

        /// <summary>
        /// Finds the link at the supplied absolute cell coordinate.
        /// </summary>
        /// <param name="x">The zero-based column.</param>
        /// <param name="y">The zero-based row.</param>
        /// <returns>The link at that position, or null.</returns>
        public Link? HitTest(int x, int y)
        {
            Point point = new Point(x, y);
            for (int i = 0; i < _Rects.Count; i++)
            {
                if (_Rects[i].Contains(point))
                    return _Links[_RectLink[i]];
            }

            return null;
        }
    }
}

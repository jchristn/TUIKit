namespace TUIKit.Content
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using TUIKit;

    /// <summary>
    /// Tracks a text selection over rendered lines as an anchor point and a moving caret point, and
    /// extracts the selected text. Coordinates are (column, row) into the line list. Selection is
    /// TUIKit's own — it does not rely on the terminal's native select, which mouse tracking disables.
    /// </summary>
    /// <remarks>Not thread-safe; drive from the input/render thread.</remarks>
    public sealed class Selection
    {
        private Point _Anchor;
        private Point _Caret;

        /// <summary>
        /// Gets a value indicating whether a selection is active.
        /// </summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// Gets the anchor point (where the selection began).
        /// </summary>
        public Point Anchor
        {
            get { return _Anchor; }
        }

        /// <summary>
        /// Gets the caret point (the moving end of the selection).
        /// </summary>
        public Point Caret
        {
            get { return _Caret; }
        }

        /// <summary>
        /// Begins a selection at the supplied point.
        /// </summary>
        /// <param name="point">The anchor point.</param>
        public void Begin(Point point)
        {
            _Anchor = point;
            _Caret = point;
            IsActive = true;
        }

        /// <summary>
        /// Moves the caret, extending the selection.
        /// </summary>
        /// <param name="point">The new caret point.</param>
        public void Extend(Point point)
        {
            if (!IsActive)
                Begin(point);
            else
                _Caret = point;
        }

        /// <summary>
        /// Clears the selection.
        /// </summary>
        public void Clear()
        {
            IsActive = false;
        }

        /// <summary>
        /// Gets the ordered start point (top-left of the selection).
        /// </summary>
        /// <returns>The start point.</returns>
        public Point OrderedStart()
        {
            return IsBefore(_Anchor, _Caret) ? _Anchor : _Caret;
        }

        /// <summary>
        /// Gets the ordered end point (bottom-right of the selection).
        /// </summary>
        /// <returns>The end point.</returns>
        public Point OrderedEnd()
        {
            return IsBefore(_Anchor, _Caret) ? _Caret : _Anchor;
        }

        /// <summary>
        /// Extracts the selected text from the supplied lines. The end column is exclusive. Newlines
        /// join multi-line selections.
        /// </summary>
        /// <param name="lines">The source lines. Must not be null.</param>
        /// <returns>The selected text, or an empty string when no selection is active.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="lines"/> is null.</exception>
        public string GetText(IReadOnlyList<string> lines)
        {
            if (lines == null)
                throw new ArgumentNullException(nameof(lines));

            if (!IsActive)
                return string.Empty;

            Point start = OrderedStart();
            Point end = OrderedEnd();

            if (start.Y < 0 || start.Y >= lines.Count)
                return string.Empty;

            if (start.Y == end.Y)
            {
                string line = lines[start.Y];
                int from = Clamp(start.X, 0, line.Length);
                int to = Clamp(end.X, 0, line.Length);
                return to > from ? line.Substring(from, to - from) : string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            string first = lines[start.Y];
            builder.Append(first.Substring(Clamp(start.X, 0, first.Length)));
            builder.Append('\n');

            for (int row = start.Y + 1; row < end.Y && row < lines.Count; row++)
            {
                builder.Append(lines[row]);
                builder.Append('\n');
            }

            if (end.Y < lines.Count)
            {
                string last = lines[end.Y];
                builder.Append(last.Substring(0, Clamp(end.X, 0, last.Length)));
            }

            return builder.ToString();
        }

        private static bool IsBefore(Point a, Point b)
        {
            if (a.Y != b.Y)
                return a.Y < b.Y;

            return a.X <= b.X;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;

            return value;
        }
    }
}

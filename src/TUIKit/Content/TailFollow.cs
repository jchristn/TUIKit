namespace TUIKit.Content
{
    using System;
    using System.Globalization;

    /// <summary>
    /// The follow-the-bottom decision for a streaming view (chat, log, build output), shared by
    /// <see cref="Pane"/> and <see cref="Widgets.ListView{T}"/>: keep showing new content while the reader
    /// is at the bottom, stop the moment they move the viewport up, count what arrives while they read,
    /// and resume when they return to the bottom.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The contract that makes it predictable: only a viewport move away from the bottom stops
    /// following (<see cref="OnViewportMoved"/>). Selecting or highlighting a row is not a viewport move,
    /// so it never stops following; a view must report scrolling here, never selection. Reaching the
    /// bottom again, or <see cref="ReturnToTail"/> (bind it to End or to any action that should show the
    /// newest content), resumes following and clears the counter.
    /// </para>
    /// <para>
    /// While not following, <see cref="IndicatorText"/> supplies the "N new below" line a view draws over
    /// its last row. Thread-safe: content may be appended from any thread.
    /// </para>
    /// </remarks>
    public sealed class TailFollow
    {
        private readonly object _Sync = new object();
        private TailFollowMode _Mode = TailFollowMode.FollowAtBottom;
        private bool _Following = true;
        private int _NewItemsBelow;
        private bool _ShowIndicator = true;
        private string _IndicatorFormat = " {0} new below ";

        /// <summary>
        /// Raised after <see cref="IsFollowing"/> changes, with the new value. Raised on the thread that
        /// caused the change, outside the internal lock.
        /// </summary>
        public event Action<bool>? FollowingChanged;

        /// <summary>
        /// Gets or sets the follow mode. Defaults to <see cref="TailFollowMode.FollowAtBottom"/>. Switching
        /// to <see cref="TailFollowMode.AlwaysFollow"/> starts following; switching to
        /// <see cref="TailFollowMode.Never"/> stops.
        /// </summary>
        public TailFollowMode Mode
        {
            get { lock (_Sync) { return _Mode; } }
            set
            {
                bool changed;
                bool following;
                lock (_Sync)
                {
                    _Mode = value;
                    if (value == TailFollowMode.AlwaysFollow)
                        _NewItemsBelow = 0;
                    changed = SetFollowingLocked(value != TailFollowMode.Never && (value == TailFollowMode.AlwaysFollow || _Following));
                    following = _Following;
                }

                if (changed)
                    FollowingChanged?.Invoke(following);
            }
        }

        /// <summary>
        /// Gets a value indicating whether appended content should bring the viewport to the bottom.
        /// Starts true (false in <see cref="TailFollowMode.Never"/>).
        /// </summary>
        public bool IsFollowing
        {
            get { lock (_Sync) { return _Following; } }
        }

        /// <summary>
        /// Gets the number of items (lines or rows) appended since following stopped. Zero while
        /// following.
        /// </summary>
        public int NewItemsBelow
        {
            get { lock (_Sync) { return _NewItemsBelow; } }
        }

        /// <summary>
        /// Gets or sets a value indicating whether views draw the "N new below" indicator while not
        /// following and new items have arrived. Defaults to true.
        /// </summary>
        public bool ShowIndicator
        {
            get { lock (_Sync) { return _ShowIndicator; } }
            set { lock (_Sync) { _ShowIndicator = value; } }
        }

        /// <summary>
        /// Gets or sets the composite format of the indicator; <c>{0}</c> is replaced by
        /// <see cref="NewItemsBelow"/>. Defaults to <c>" {0} new below "</c>. Must contain <c>{0}</c>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        /// <exception cref="ArgumentException">Thrown when the value does not contain <c>{0}</c> or is not a
        /// valid composite format.</exception>
        public string IndicatorFormat
        {
            get { lock (_Sync) { return _IndicatorFormat; } }
            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));
                if (value.IndexOf("{0}", StringComparison.Ordinal) < 0)
                    throw new ArgumentException("Indicator format must contain {0}.", nameof(value));

                try
                {
                    string.Format(CultureInfo.InvariantCulture, value, 1);
                }
                catch (FormatException ex)
                {
                    throw new ArgumentException("Indicator format is not a valid composite format: " + ex.Message, nameof(value), ex);
                }

                lock (_Sync) { _IndicatorFormat = value; }
            }
        }

        /// <summary>
        /// Gets the indicator text to draw now, or null when none should show (following, nothing new,
        /// or <see cref="ShowIndicator"/> off).
        /// </summary>
        public string? IndicatorText
        {
            get
            {
                lock (_Sync)
                {
                    if (_Following || _NewItemsBelow <= 0 || !_ShowIndicator)
                        return null;

                    return string.Format(CultureInfo.InvariantCulture, _IndicatorFormat, _NewItemsBelow);
                }
            }
        }

        /// <summary>
        /// Reports that the viewport moved (scrolling, paging, a search jump, or the view keeping a
        /// selection visible). At the bottom (<paramref name="offset"/> at or past
        /// <paramref name="maxOffset"/>) following resumes and the counter clears; above it, following stops
        /// in <see cref="TailFollowMode.FollowAtBottom"/> mode. <see cref="TailFollowMode.AlwaysFollow"/>
        /// keeps following regardless. Do not call this for selection changes that leave the viewport
        /// where it is.
        /// </summary>
        /// <param name="offset">The first visible row, zero-based. Must be zero or greater.</param>
        /// <param name="maxOffset">The first visible row when scrolled fully to the bottom. Must be zero or greater.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when either value is negative.</exception>
        public void OnViewportMoved(int offset, int maxOffset)
        {
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset), offset, "Offset must be zero or greater.");
            if (maxOffset < 0)
                throw new ArgumentOutOfRangeException(nameof(maxOffset), maxOffset, "Maximum offset must be zero or greater.");

            bool changed;
            bool following;
            lock (_Sync)
            {
                bool atBottom = offset >= maxOffset;
                if (atBottom)
                    _NewItemsBelow = 0;

                bool next;
                switch (_Mode)
                {
                    case TailFollowMode.AlwaysFollow:
                        next = true;
                        break;
                    case TailFollowMode.Never:
                        next = false;
                        break;
                    default:
                        next = atBottom;
                        break;
                }

                changed = SetFollowingLocked(next);
                following = _Following;
            }

            if (changed)
                FollowingChanged?.Invoke(following);
        }

        /// <summary>
        /// Reports that items were appended and returns whether the view should bring the viewport to the
        /// bottom. While not following, the items are added to <see cref="NewItemsBelow"/>.
        /// </summary>
        /// <param name="count">The number of items appended. Must be zero or greater; zero changes nothing.</param>
        /// <returns><c>true</c> when the view should show the bottom; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count"/> is negative.</exception>
        public bool OnContentAppended(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), count, "Appended count must be zero or greater.");

            lock (_Sync)
            {
                if (_Following)
                    return true;

                _NewItemsBelow += count;
                return false;
            }
        }

        /// <summary>
        /// Resumes following and clears the counter, as when the user presses End or clicks the
        /// indicator. The caller then shows the bottom. In <see cref="TailFollowMode.Never"/> mode the counter
        /// clears but following does not resume.
        /// </summary>
        public void ReturnToTail()
        {
            bool changed;
            bool following;
            lock (_Sync)
            {
                _NewItemsBelow = 0;
                changed = SetFollowingLocked(_Mode != TailFollowMode.Never);
                following = _Following;
            }

            if (changed)
                FollowingChanged?.Invoke(following);
        }

        /// <summary>
        /// Returns to the initial state, as when a view is cleared: following (unless the mode is
        /// <see cref="TailFollowMode.Never"/>) with nothing counted.
        /// </summary>
        public void Reset()
        {
            ReturnToTail();
        }

        /// <summary>
        /// Reports that items counted in <see cref="NewItemsBelow"/> were removed (deleted, or collapsed
        /// away) before the reader saw them, so the "N new below" count does not overstate what is left. The
        /// count goes down by <paramref name="removedBelow"/> and never below zero. Use the same unit as
        /// <see cref="OnContentAppended"/>: <see cref="Pane"/> counts lines and calls this when a new line
        /// below the viewport is removed through its <see cref="PaneLineHandle"/>. Does not change
        /// <see cref="IsFollowing"/>. Thread-safe.
        /// </summary>
        /// <param name="removedBelow">The number of new items removed. Must be zero or greater; zero
        /// changes nothing.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="removedBelow"/> is negative.</exception>
        public void OnContentRemoved(int removedBelow)
        {
            if (removedBelow < 0)
                throw new ArgumentOutOfRangeException(nameof(removedBelow), removedBelow, "Removed count must be zero or greater.");

            lock (_Sync)
                _NewItemsBelow = Math.Max(0, _NewItemsBelow - removedBelow);
        }

        /// <summary>
        /// Reports a deliberate jump away from the tail, such as going to a search hit or a bookmark: the
        /// view stops following (except in <see cref="TailFollowMode.AlwaysFollow"/>) and the
        /// <see cref="NewItemsBelow"/> count is cleared, since the reader is now looking at chosen content.
        /// Raises <see cref="FollowingChanged"/> when following changes. <see cref="ReturnToTail"/> (End)
        /// re-attaches. Custom scrolling widgets call it for their own jumps; <see cref="Pane"/> calls it
        /// for <see cref="Pane.FindNext"/> and <see cref="Pane.FindPrevious"/>. Thread-safe.
        /// </summary>
        public void OnJumpedAway()
        {
            bool changed;
            bool following;
            lock (_Sync)
            {
                _NewItemsBelow = 0;
                changed = SetFollowingLocked(_Mode == TailFollowMode.AlwaysFollow);
                following = _Following;
            }

            if (changed)
                FollowingChanged?.Invoke(following);
        }

        internal void DetachAtJump()
        {
            OnJumpedAway();
        }

        private bool SetFollowingLocked(bool value)
        {
            if (_Following == value)
                return false;

            _Following = value;
            return true;
        }
    }
}

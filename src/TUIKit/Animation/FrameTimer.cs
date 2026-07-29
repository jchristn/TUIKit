namespace TUIKit.Animation
{
    using System;
    using System.Collections.Generic;
    using TUIKit.Reactive;

    /// <summary>
    /// A deterministic, tick-driven scheduler for animations and periodic work. The timer holds no
    /// wall clock; the host advances it once per frame with <see cref="Tick"/>, passing the elapsed
    /// milliseconds. Because time is injected, timers are fully reproducible in tests. Register
    /// repeating work with <see cref="Every"/> and one-shots with <see cref="After"/>; dispose the
    /// returned handle to cancel. All members are thread-safe.
    /// </summary>
    public sealed class FrameTimer
    {
        private readonly object _Lock = new object();
        private readonly List<TimerEntry> _Entries = new List<TimerEntry>();

        /// <summary>
        /// Gets the number of active (not yet cancelled or completed) timers.
        /// </summary>
        public int ActiveCount
        {
            get
            {
                lock (_Lock)
                {
                    int count = 0;
                    for (int i = 0; i < _Entries.Count; i++)
                    {
                        if (!_Entries[i].Cancelled)
                            count++;
                    }

                    return count;
                }
            }
        }

        /// <summary>
        /// Schedules a callback to run every <paramref name="intervalMs"/> milliseconds of ticked time.
        /// </summary>
        /// <param name="intervalMs">The interval in milliseconds. Must be positive.</param>
        /// <param name="callback">The callback. Must not be null.</param>
        /// <returns>A handle that cancels the timer when disposed.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="intervalMs"/> is not positive.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="callback"/> is null.</exception>
        public IDisposable Every(double intervalMs, Action callback)
        {
            return Schedule(intervalMs, true, callback);
        }

        /// <summary>
        /// Schedules a callback to run once, <paramref name="delayMs"/> milliseconds of ticked time
        /// from now.
        /// </summary>
        /// <param name="delayMs">The delay in milliseconds. Must be positive.</param>
        /// <param name="callback">The callback. Must not be null.</param>
        /// <returns>A handle that cancels the timer when disposed.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="delayMs"/> is not positive.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="callback"/> is null.</exception>
        public IDisposable After(double delayMs, Action callback)
        {
            return Schedule(delayMs, false, callback);
        }

        /// <summary>
        /// Advances the timer by the given elapsed time, firing any callbacks that come due. A single
        /// tick larger than an interval fires a repeating callback only once (frames are not
        /// "caught up"), which avoids runaway bursts after a long stall.
        /// </summary>
        /// <param name="deltaMs">The elapsed time since the previous tick, in milliseconds.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="deltaMs"/> is negative.</exception>
        public void Tick(double deltaMs)
        {
            if (deltaMs < 0)
                throw new ArgumentOutOfRangeException(nameof(deltaMs));

            List<Action> due = new List<Action>();
            lock (_Lock)
            {
                for (int i = 0; i < _Entries.Count; i++)
                {
                    TimerEntry entry = _Entries[i];
                    if (entry.Cancelled)
                        continue;

                    entry.Accumulated += deltaMs;
                    if (entry.Accumulated < entry.IntervalMs)
                        continue;

                    due.Add(entry.Callback);
                    if (entry.Repeat)
                        entry.Accumulated -= entry.IntervalMs;
                    else
                        entry.Cancelled = true;
                }

                _Entries.RemoveAll(e => e.Cancelled);
            }

            for (int i = 0; i < due.Count; i++)
                due[i]();
        }

        private IDisposable Schedule(double intervalMs, bool repeat, Action callback)
        {
            if (intervalMs <= 0)
                throw new ArgumentOutOfRangeException(nameof(intervalMs));
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            TimerEntry entry = new TimerEntry(intervalMs, repeat, callback);
            lock (_Lock)
            {
                _Entries.Add(entry);
            }

            return new Subscription(() => Cancel(entry));
        }

        private void Cancel(TimerEntry entry)
        {
            lock (_Lock)
            {
                entry.Cancelled = true;
                _Entries.Remove(entry);
            }
        }
    }
}

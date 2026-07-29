namespace TUIKit.Animation
{
    using System;

    /// <summary>
    /// A scheduled callback tracked by a <see cref="FrameTimer"/>: its interval, accumulated time, and
    /// whether it repeats.
    /// </summary>
    internal sealed class TimerEntry
    {
        internal double IntervalMs { get; }

        internal bool Repeat { get; }

        internal Action Callback { get; }

        internal double Accumulated;

        internal bool Cancelled;

        internal TimerEntry(double intervalMs, bool repeat, Action callback)
        {
            IntervalMs = intervalMs;
            Repeat = repeat;
            Callback = callback;
        }
    }
}

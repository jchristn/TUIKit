namespace TUIKit.Example
{
    using System;
    using System.Collections.Generic;
    using TUIKit.Modals;

    /// <summary>
    /// Shared, thread-safe state between the simulated agent (which runs on a background thread) and
    /// the render thread: the CPU history sparkline series, the current tool progress, and a reference
    /// to the notification center and clock.
    /// </summary>
    internal sealed class HarnessState
    {
        private readonly object _Sync = new object();
        private readonly List<double> _Cpu = new List<double>();
        private readonly Func<long> _Clock;

        internal NotificationCenter Notifications { get; }

        internal bool ActiveTool { get; set; }

        internal double ToolProgress { get; set; }

        internal HarnessState(NotificationCenter notifications, Func<long> clock)
        {
            Notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
            _Clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        internal long NowMilliseconds()
        {
            return _Clock();
        }

        internal void PushCpu(double value)
        {
            lock (_Sync)
            {
                _Cpu.Add(value);
                while (_Cpu.Count > 60)
                    _Cpu.RemoveAt(0);
            }
        }

        internal double CurrentCpu()
        {
            lock (_Sync)
            {
                return _Cpu.Count > 0 ? _Cpu[_Cpu.Count - 1] : 0.0;
            }
        }

        internal double[] CpuSnapshot()
        {
            lock (_Sync)
            {
                return _Cpu.ToArray();
            }
        }
    }
}

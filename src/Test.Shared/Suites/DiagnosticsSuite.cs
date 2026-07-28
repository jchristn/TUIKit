namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Diagnostics;
    using TUIKit.Terminal;
    using TUIKit.Testing;
    using TUIKit.Widgets;

    /// <summary>
    /// Touchstone suite covering frame statistics, input record/replay, and the snapshot tooling.
    /// </summary>
    public static class DiagnosticsSuite
    {
        /// <summary>
        /// Builds the diagnostics suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Diagnostics",
                displayName: "Diagnostics and Snapshot",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Diagnostics", "FrameStats", "Frame stats average and FPS",
                        _ =>
                        {
                            FrameStats stats = new FrameStats(10);
                            stats.Record(10);
                            stats.Record(20);
                            stats.Record(30);
                            Check.Equal(20.0, stats.AverageMilliseconds(), "Average of 10/20/30");
                            Check.Equal(30.0, stats.LastMilliseconds, "Last frame");
                            Check.Equal(3, (int)stats.FrameCount, "Frame count");
                            Check.True(stats.FramesPerSecond() > 49.0 && stats.FramesPerSecond() < 51.0, "About 50 fps");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Diagnostics", "InputReplay", "Recording replays into a backend",
                        _ =>
                        {
                            InputRecording recording = new InputRecording();
                            recording.Add(Encode("ab"), 0);
                            recording.Add(Encode("c"), 50);
                            Check.Equal(2, recording.Count, "Two chunks");
                            Check.Equal(50, recording.DelayAt(1), "Delay recorded");

                            HeadlessBackend backend = new HeadlessBackend();
                            recording.Replay(backend);
                            byte[] buffer = new byte[10];
                            int read = backend.ReadInput(buffer, 0, buffer.Length);
                            Check.Equal(3, read, "All bytes replayed");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Diagnostics", "SnapshotWidget", "Snapshot renders a widget to text",
                        _ =>
                        {
                            Gauge gauge = new Gauge();
                            gauge.Value = 1.0;
                            string text = Snapshot.RenderWidget(gauge, 4, 1);
                            Check.Equal("████", text, "Full gauge snapshot");
                            return Task.CompletedTask;
                        })
                });
        }

        private static byte[] Encode(string text)
        {
            return Encoding.UTF8.GetBytes(text);
        }
    }
}

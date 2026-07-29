namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Diagnostics;
    using TUIKit.Layout;
    using TUIKit.Testing;
    using TUIKit.Widgets;

    /// <summary>
    /// Validation coverage for the diagnostics and testing helpers: frame statistics, input
    /// record/replay, the debug overlay, snapshots, and the widget test harness.
    /// </summary>
    public static class DiagnosticsValidationSuite
    {
        /// <summary>
        /// Builds the diagnostics/testing validation suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "DiagnosticsValidation",
                displayName: "Diagnostics & Testing Validation",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("DiagnosticsValidation", "FrameStats", "Frame stats validate window and samples",
                        _ =>
                        {
                            Check.Throws<ArgumentOutOfRangeException>(() => new FrameStats(0), "non-positive window");
                            FrameStats stats = new FrameStats(8);
                            Check.Throws<ArgumentOutOfRangeException>(() => stats.Record(-1), "negative sample");
                            stats.Record(16);
                            Check.Equal(16.0, stats.LastMilliseconds, "recorded sample");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("DiagnosticsValidation", "InputRecording", "Input recording validates data, delays, and replay",
                        _ =>
                        {
                            InputRecording recording = new InputRecording();
                            Check.Throws<ArgumentNullException>(() => recording.Add(null!, 0), "null data");
                            Check.Throws<ArgumentOutOfRangeException>(() => recording.Add(new byte[1], -1), "negative delay");
                            recording.Add(new byte[] { 65 }, 10);
                            Check.Equal(10, recording.DelayAt(0), "delay stored");
                            Check.Throws<ArgumentOutOfRangeException>(() => recording.DelayAt(99), "delay index out of range");
                            Check.Throws<ArgumentNullException>(() => recording.Replay(null!), "replay null backend");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("DiagnosticsValidation", "OverlayAndSnapshot", "Debug overlay and snapshot null-guard and bound their arguments",
                        _ =>
                        {
                            Layout layout = Layout.Column(LayoutSlot.Fill("a"));
                            FrameStats stats = new FrameStats(8);
                            Check.Throws<ArgumentNullException>(() => DebugOverlay.Render(null!, layout, stats), "overlay null surface");

                            Check.Throws<ArgumentNullException>(() => Snapshot.ToText(null!), "snapshot null buffer");
                            Check.Throws<ArgumentNullException>(() => Snapshot.RenderWidget(null!, 5, 5), "snapshot null widget");
                            Check.Throws<ArgumentOutOfRangeException>(() => Snapshot.RenderWidget(new Label(Text.From("x")), 0, 5), "snapshot zero width");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("DiagnosticsValidation", "WidgetTester", "Widget tester validates construction and assertions",
                        _ =>
                        {
                            Check.Throws<ArgumentNullException>(() => WidgetTester.For(null!, 5, 5), "null widget");
                            Check.Throws<ArgumentOutOfRangeException>(() => WidgetTester.For(new Label(Text.From("x")), 0, 5), "zero width");

                            WidgetTester tester = WidgetTester.For(new Label(Text.From("hello")), 12, 1).Render();
                            Check.Throws<ArgumentOutOfRangeException>(() => tester.Row(999), "row out of range");
                            Check.Throws<ArgumentNullException>(() => tester.Type(null!), "type null");
                            Check.Throws<InvalidOperationException>(() => tester.AssertNotContains("hello"), "assert-not-contains fails on present text");
                            tester.AssertContains("hello");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}

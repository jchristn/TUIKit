namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Animation;
    using TUIKit.Content;
    using TUIKit.Input;
    using TUIKit.Widgets;

    /// <summary>
    /// Touchstone suite hardening value validation across the library: every range-constrained input
    /// is either clamped to sensible bounds or rejects out-of-range values with
    /// <see cref="ArgumentOutOfRangeException"/>, and defaults are exercised.
    /// </summary>
    public static class ValueValidationSuite
    {
        /// <summary>
        /// Builds the validation suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Validation",
                displayName: "Value Validation (clamp / throw / defaults)",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Validation", "GaugeClamp", "Gauge value defaults to 0 and clamps to [0,1]",
                        _ =>
                        {
                            Gauge gauge = new Gauge();
                            Check.Equal(0.0, gauge.Value, "default value is zero");
                            gauge.Value = 0.5;
                            Check.Equal(0.5, gauge.Value, "in-range value kept");
                            gauge.Value = -3.0;
                            Check.Equal(0.0, gauge.Value, "below-range clamped up to 0");
                            gauge.Value = 9.0;
                            Check.Equal(1.0, gauge.Value, "above-range clamped down to 1");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Validation", "ProgressBarClamp", "Progress bar value defaults to 0 and clamps to [0,1]",
                        _ =>
                        {
                            ProgressBar bar = new ProgressBar();
                            Check.Equal(0.0, bar.Value, "default value is zero");
                            bar.Value = 1.0;
                            Check.Equal(1.0, bar.Value, "one kept");
                            bar.Value = 2.5;
                            Check.Equal(1.0, bar.Value, "clamped to 1");
                            bar.Value = -0.5;
                            Check.Equal(0.0, bar.Value, "clamped to 0");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Validation", "ProgressTaskClamp", "Progress task clamps and reports completion",
                        _ =>
                        {
                            ProgressTask task = new ProgressTask("job");
                            Check.Equal(0.0, task.Value, "default zero");
                            Check.False(task.IsComplete, "not complete at zero");
                            task.Report(0.5);
                            Check.Equal(0.5, task.Value, "reported value kept");
                            task.Report(4.0);
                            Check.Equal(1.0, task.Value, "reported value clamped to 1");
                            Check.True(task.IsComplete, "complete at 1");
                            task.Report(-2.0);
                            Check.Equal(0.0, task.Value, "reported value clamped to 0");
                            Check.Throws<ArgumentNullException>(() => new ProgressTask(null!), "null label");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Validation", "SparklineCapacity", "Sparkline push trims to capacity and rejects non-positive",
                        _ =>
                        {
                            Sparkline spark = new Sparkline();
                            for (int i = 0; i < 10; i++)
                                spark.Push(i, 5);

                            CellBuffer buffer = new CellBuffer(20, 1);
                            spark.Render(new BufferSurface(buffer)); // renders at most 5 kept values without error

                            Check.Throws<ArgumentOutOfRangeException>(() => spark.Push(1, 0), "zero capacity");
                            Check.Throws<ArgumentOutOfRangeException>(() => spark.Push(1, -3), "negative capacity");
                            Check.Throws<ArgumentNullException>(() => spark.SetValues(null!), "null values");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Validation", "ScrollViewBounds", "Scroll view rejects bad sizes and clamps scroll at 0",
                        _ =>
                        {
                            Pane pane = new Pane("p");
                            pane.WriteLine("x");
                            ScrollView view = new ScrollView(pane, 10, 10);

                            view.ScrollBy(-100, -100);
                            CellBuffer buffer = new CellBuffer(8, 4);
                            view.Render(new BufferSurface(buffer)); // must not throw at origin

                            view.ScrollTo(-5, -5); // clamps to 0,0
                            view.Render(new BufferSurface(buffer));

                            Check.Throws<ArgumentNullException>(() => new ScrollView(null!, 5, 5), "null child");
                            Check.Throws<ArgumentOutOfRangeException>(() => new ScrollView(pane, 0, 5), "zero content width");
                            Check.Throws<ArgumentOutOfRangeException>(() => new ScrollView(pane, 5, -1), "negative content height");
                            Check.Throws<ArgumentOutOfRangeException>(() => view.SetContentSize(0, 4), "zero width");
                            Check.Throws<ArgumentOutOfRangeException>(() => view.SetContentSize(4, 0), "zero height");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Validation", "SplitRatioClamp", "Split view clamps ratios and validates the resize step",
                        _ =>
                        {
                            SplitView split = new SplitView(SplitOrientation.Horizontal,
                                new Label(Text.From("a")), new Label(Text.From("b")), 0.5);

                            split.MinRatio = -1.0;
                            Check.Equal(0.0, split.MinRatio, "min ratio clamped up to 0");
                            split.MaxRatio = 5.0;
                            Check.Equal(1.0, split.MaxRatio, "max ratio clamped down to 1");

                            split.MinRatio = 0.3;
                            split.MaxRatio = 0.7;
                            for (int i = 0; i < 40; i++)
                                split.HandleKey(KeyEvent.Special(KeyCode.Right));
                            Check.True(split.Ratio <= 0.7 + 1e-9, "ratio clamped to max");
                            for (int i = 0; i < 40; i++)
                                split.HandleKey(KeyEvent.Special(KeyCode.Left));
                            Check.True(split.Ratio >= 0.3 - 1e-9, "ratio clamped to min");

                            split.ResizeStep = 0.2;
                            Check.Equal(0.2, split.ResizeStep, "valid step kept");
                            Check.Throws<ArgumentOutOfRangeException>(() => split.ResizeStep = 0.0, "zero step");
                            Check.Throws<ArgumentOutOfRangeException>(() => split.ResizeStep = -0.1, "negative step");
                            Check.Throws<ArgumentOutOfRangeException>(() => split.ResizeStep = 1.5, "step over one");

                            SplitView high = new SplitView(SplitOrientation.Vertical, new Label(Text.From("a")), new Label(Text.From("b")), 5.0);
                            Check.True(high.Ratio <= 0.9 + 1e-9, "constructor ratio clamped to default max");
                            SplitView low = new SplitView(SplitOrientation.Vertical, new Label(Text.From("a")), new Label(Text.From("b")), -3.0);
                            Check.True(low.Ratio >= 0.1 - 1e-9, "constructor ratio clamped to default min");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Validation", "ColorPickerClamp", "Color picker clamps channels to [0,255]",
                        _ =>
                        {
                            ColorPicker picker = new ColorPicker(Color.FromRgb(0, 0, 0));
                            picker.HandleKey(KeyEvent.Special(KeyCode.End));
                            Check.Equal(255, picker.ActiveValue, "end snaps to 255");
                            picker.HandleKey(KeyEvent.Special(KeyCode.Right));
                            Check.Equal(255, picker.ActiveValue, "right at 255 stays clamped");
                            picker.HandleKey(KeyEvent.Special(KeyCode.PageUp));
                            Check.Equal(255, picker.ActiveValue, "page up at 255 stays clamped");

                            picker.HandleKey(KeyEvent.Special(KeyCode.Home));
                            Check.Equal(0, picker.ActiveValue, "home snaps to 0");
                            picker.HandleKey(KeyEvent.Special(KeyCode.Left));
                            Check.Equal(0, picker.ActiveValue, "left at 0 stays clamped");
                            picker.HandleKey(KeyEvent.Special(KeyCode.PageDown));
                            Check.Equal(0, picker.ActiveValue, "page down at 0 stays clamped");
                            Check.Equal(0, picker.Value.R, "composed red is zero after clamping down");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Validation", "BrailleBounds", "Braille canvas validates size and ignores out-of-range pixels",
                        _ =>
                        {
                            Check.Throws<ArgumentOutOfRangeException>(() => new BrailleCanvas(0, 4), "zero width");
                            Check.Throws<ArgumentOutOfRangeException>(() => new BrailleCanvas(4, -2), "negative height");

                            BrailleCanvas canvas = new BrailleCanvas(2, 2);
                            canvas.Set(-1, -1); // ignored
                            canvas.Set(1000, 1000); // ignored
                            canvas.Unset(500, 500); // ignored
                            Check.False(canvas.Get(-1, 0), "negative coord reads false");
                            Check.False(canvas.Get(1000, 1000), "out-of-range coord reads false");
                            canvas.Line(0, 0, 3, 7); // spans the canvas without throwing
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Validation", "TimerAndTween", "Frame timer and tween validate their arguments",
                        _ =>
                        {
                            FrameTimer timer = new FrameTimer();
                            Check.Throws<ArgumentOutOfRangeException>(() => timer.Every(0, () => { }), "zero interval");
                            Check.Throws<ArgumentOutOfRangeException>(() => timer.After(-1, () => { }), "negative delay");
                            Check.Throws<ArgumentNullException>(() => timer.Every(10, null!), "null callback");
                            Check.Throws<ArgumentOutOfRangeException>(() => timer.Tick(-0.5), "negative tick");
                            timer.Tick(0); // zero tick is allowed

                            Tween tween = new Tween(0, 10, 100);
                            Check.Equal(0.0, tween.ValueAt(-50), "negative elapsed clamps to start");
                            Check.Equal(10.0, tween.ValueAt(500), "past-duration clamps to end");
                            Check.Throws<ArgumentOutOfRangeException>(() => new Tween(0, 1, 0), "zero duration");
                            Check.Throws<ArgumentOutOfRangeException>(() => new Tween(0, 1, -5), "negative duration");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Validation", "CellBufferBounds", "Cell buffer validates size and coordinates",
                        _ =>
                        {
                            Check.Throws<ArgumentOutOfRangeException>(() => new CellBuffer(-1, 4), "negative width");
                            Check.Throws<ArgumentOutOfRangeException>(() => new CellBuffer(4, -1), "negative height");

                            CellBuffer buffer = new CellBuffer(3, 2);
                            Check.Throws<ArgumentOutOfRangeException>(() => buffer.Get(3, 0), "x out of range");
                            Check.Throws<ArgumentOutOfRangeException>(() => buffer.Get(0, 2), "y out of range");
                            Check.Throws<ArgumentOutOfRangeException>(() => buffer.Get(-1, 0), "negative x");

                            CellBuffer empty = new CellBuffer(0, 0); // zero is allowed
                            Check.Equal(0, empty.Width, "zero-size buffer allowed");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}

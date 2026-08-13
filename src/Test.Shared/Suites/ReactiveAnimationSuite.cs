namespace Test.Shared.Suites
{
    using System;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Animation;
    using TUIKit.Input;
    using TUIKit.Reactive;
    using TUIKit.Testing;
    using TUIKit.Widgets;

    /// <summary>
    /// Touchstone suite for reactive, animation, and testing: observable data binding (10.30), easing/tween/timer animation
    /// (10.33), the multi-task progress widget (10.24), and the widget test harness (10.23).
    /// </summary>
    public static class ReactiveAnimationSuite
    {
        /// <summary>
        /// Builds the reactive, animation, and testing suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "ReactiveAnimation",
                displayName: "Reactive, Animation & Testing",
                cases: new System.Collections.Generic.List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("ReactiveAnimation", "Observable", "Observable notifies subscribers only on real change",
                        _ =>
                        {
                            Observable<int> value = new Observable<int>(1);
                            int fires = 0;
                            int last = 0;
                            IDisposable sub = value.Subscribe(v => { fires++; last = v; });

                            value.Value = 1; // no change
                            Check.Equal(0, fires, "equal assignment does not notify");
                            value.Value = 2;
                            Check.Equal(1, fires, "change notifies once");
                            Check.Equal(2, last, "handler received new value");

                            sub.Dispose();
                            value.Value = 3;
                            Check.Equal(1, fires, "disposed subscriber stops receiving");
                            Check.Equal(3, value.Value, "value still updates");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ReactiveAnimation", "ObservableBind", "Bind pushes current and future values",
                        _ =>
                        {
                            Observable<string> name = new Observable<string>("a");
                            string mirror = string.Empty;
                            name.Bind(v => mirror = v);
                            Check.Equal("a", mirror, "bind delivers current value immediately");
                            name.Value = "b";
                            Check.Equal("b", mirror, "bind delivers changes");

                            Check.Throws<ArgumentNullException>(() => name.Subscribe(null!), "null subscriber");
                            Check.Throws<ArgumentNullException>(() => name.Bind(null!), "null setter");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ReactiveAnimation", "Tween", "Tween interpolates and clamps with easing",
                        _ =>
                        {
                            Tween tween = new Tween(0, 100, 1000, Easing.Linear);
                            Check.Equal(0.0, tween.ValueAt(0), "starts at from");
                            Check.Equal(50.0, tween.ValueAt(500), "midpoint is linear");
                            Check.Equal(100.0, tween.ValueAt(1000), "ends at to");
                            Check.Equal(100.0, tween.ValueAt(5000), "clamps past the end");
                            Check.True(tween.IsComplete(1000), "complete at duration");
                            Check.False(tween.IsComplete(999), "not complete before duration");

                            Check.True(Easing.EaseInQuad(0.5) < 0.5, "ease-in lags at the midpoint");
                            Check.True(Easing.EaseOutQuad(0.5) > 0.5, "ease-out leads at the midpoint");
                            Check.Throws<ArgumentOutOfRangeException>(() => new Tween(0, 1, 0), "zero duration");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ReactiveAnimation", "FrameTimer", "Frame timer fires repeating and one-shot callbacks",
                        _ =>
                        {
                            FrameTimer timer = new FrameTimer();
                            int repeats = 0;
                            int once = 0;
                            timer.Every(100, () => repeats++);
                            IDisposable oneShot = timer.After(250, () => once++);
                            Check.Equal(2, timer.ActiveCount, "two timers active");

                            timer.Tick(100);
                            Check.Equal(1, repeats, "repeating fired at 100");
                            timer.Tick(100);
                            Check.Equal(2, repeats, "repeating fired at 200");
                            timer.Tick(100); // total 300 -> one-shot due
                            Check.Equal(3, repeats, "repeating fired at 300");
                            Check.Equal(1, once, "one-shot fired once");
                            Check.Equal(1, timer.ActiveCount, "one-shot removed after firing");

                            timer.Tick(1000);
                            Check.Equal(1, once, "one-shot does not fire again");

                            Check.Throws<ArgumentOutOfRangeException>(() => timer.Every(0, () => { }), "zero interval");
                            Check.Throws<ArgumentNullException>(() => timer.After(10, null!), "null callback");
                            Check.Throws<ArgumentOutOfRangeException>(() => timer.Tick(-1), "negative tick");
                            oneShot.Dispose();
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ReactiveAnimation", "TimerCancel", "Disposing a timer handle cancels it",
                        _ =>
                        {
                            FrameTimer timer = new FrameTimer();
                            int fires = 0;
                            IDisposable handle = timer.Every(50, () => fires++);
                            timer.Tick(50);
                            Check.Equal(1, fires, "fired before cancel");
                            handle.Dispose();
                            Check.Equal(0, timer.ActiveCount, "no active timers after dispose");
                            timer.Tick(500);
                            Check.Equal(1, fires, "cancelled timer stops firing");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ReactiveAnimation", "MultiProgress", "Multi-progress renders bars and percentages",
                        _ =>
                        {
                            MultiProgress progress = new MultiProgress();
                            ProgressTask download = progress.Add("download");
                            ProgressTask verify = progress.Add("verify", 1.0);
                            Check.Equal(2, progress.Count, "two tasks");
                            download.Report(0.5);
                            Check.True(verify.IsComplete, "second task complete");

                            WidgetTester tester = WidgetTester.For(progress, 30, 2).Render();
                            tester.AssertContains("download").AssertContains("50%");
                            tester.AssertContains("verify").AssertContains("100%");

                            download.Report(5);
                            Check.Equal(1.0, download.Value, "report clamps above one");
                            download.Report(-5);
                            Check.Equal(0.0, download.Value, "report clamps below zero");
                            Check.Throws<ArgumentNullException>(() => progress.Add(null!), "null label");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ReactiveAnimation", "WidgetTester", "Widget tester drives keys and reads output",
                        _ =>
                        {
                            FuzzyList<string> list = new FuzzyList<string>(new[] { "alpha", "beta", "gamma" });
                            WidgetTester tester = WidgetTester.For(list, 20, 5);

                            tester.Type("al").Render();
                            tester.AssertContains("alpha").AssertNotContains("beta");
                            Check.True(tester.LastKeyHandled, "focusable consumed the key");

                            tester.Press(KeyCode.Backspace).Press(KeyCode.Backspace).Render();
                            tester.AssertContains("beta");

                            Check.Throws<ArgumentNullException>(() => WidgetTester.For(null!, 5, 5), "null widget");
                            Check.Throws<ArgumentOutOfRangeException>(() => WidgetTester.For(list, 0, 5), "zero width");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}

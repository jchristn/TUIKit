namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Hosting;
    using TUIKit.Layout;
    using TUIKit.Modals;
    using TUIKit.Terminal;
    using TUIKit.Testing;
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for <see cref="FocusAudit"/>: a correct layout audits clean, and each defect it exists to
    /// catch is reported (invisible focus, a Tab trap, an asymmetric container, a layout that shifts with
    /// focus, a stop with no focusable leaf, a ring that does not close within the limit).
    /// </summary>
    public static class FocusAuditSuite
    {
        /// <summary>
        /// Builds the focus audit suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "FocusAudit",
                displayName: "Focus Audit",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("FocusAudit", "CleanLayout", "Three framed regions with a nested split audit clean and the stop count matches",
                        _ =>
                        {
                            using (TuiApplication app = Framed(true))
                            {
                                FocusPath start = app.CurrentFocusPath;
                                FocusAuditResult result = FocusAudit.Run(app);
                                result.ThrowIfProblems();
                                Check.True(result.IsClean, "clean");
                                Check.Equal(4, result.Stops, "list, split left, split right, field");
                                Check.True(start.Equals(app.CurrentFocusPath), "focus returned to the start");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusAudit", "SingleStop", "An application with one focusable region audits clean with one stop",
                        _ =>
                        {
                            using (TuiApplication app = new TuiApplication(new HeadlessBackend(20, 5)))
                            {
                                app.HighlightFocusedRegion = true;
                                app.Layout = Layout.Create().Add("only", r => r.FillWidth().FillHeight().WithPadding(0).WithBorder(BorderStyle.Line)).Build();
                                app.Bind("only", new ListView<string>());
                                app.Start();
                                FocusAuditResult result = FocusAudit.Run(app);
                                Check.True(result.IsClean, "clean");
                                Check.Equal(1, result.Stops, "one stop");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusAudit", "InvisibleFocus", "Without a focus treatment every stop reports NoFocusIndicator",
                        _ =>
                        {
                            using (TuiApplication app = Framed(false))
                            {
                                FocusAuditResult result = FocusAudit.Run(app);
                                Check.Equal(4, Count(result, FocusAuditProblemKind.NoFocusIndicator), "one per stop");
                                Check.Throws<InvalidOperationException>(() => result.ThrowIfProblems(), "ThrowIfProblems throws");

                                FocusAuditResult relaxed = FocusAudit.Run(app, new FocusAuditOptions { CheckFocusIndicator = false });
                                Check.True(relaxed.IsClean, "clean when the indicator check is off");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusAudit", "TabTrap", "A widget that swallows Tab reports TraversalStuck",
                        _ =>
                        {
                            using (TuiApplication app = TwoRegions(new TabTrapWidget(), new ListView<string>()))
                            {
                                FocusAuditResult result = FocusAudit.Run(app);
                                Check.Equal(1, Count(result, FocusAuditProblemKind.TraversalStuck), "stuck reported");
                                Check.Equal(0, Count(result, FocusAuditProblemKind.TraversalDidNotCycle), "not also reported as no cycle");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusAudit", "Asymmetric", "A container whose Shift+Tab skips a child reports TraversalNotSymmetric",
                        _ =>
                        {
                            using (TuiApplication app = TwoRegions(new ListView<string>(), new SkippingScope()))
                            {
                                FocusAuditResult result = FocusAudit.Run(app);
                                Check.Equal(4, result.Stops, "list plus three children");
                                Check.Equal(1, Count(result, FocusAuditProblemKind.TraversalNotSymmetric), "asymmetry reported");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusAudit", "LayoutShift", "A layout that changes with focus reports LayoutShifted",
                        _ =>
                        {
                            using (TuiApplication app = TwoRegions(new ListView<string>(), new ListView<string>()))
                            {
                                Layout normal = app.Layout!;
                                Layout wide = Layout.Create()
                                    .Add("a", r => r.ProportionalWidth(0, 0.7).FillHeight().WithPadding(0).WithBorder(BorderStyle.Line))
                                    .Add("b", r => r.ProportionalWidth(0.7, 0.3).FillHeight().WithPadding(0).WithBorder(BorderStyle.Line))
                                    .Build();
                                app.FocusChanged += region => app.Layout = region == "b" ? wide : normal;
                                FocusAuditResult result = FocusAudit.Run(app);
                                Check.True(Count(result, FocusAuditProblemKind.LayoutShifted) > 0, "shift reported");
                                Check.True(FocusAudit.Run(app, new FocusAuditOptions { CheckLayoutStable = false }).IsClean, "clean when the layout check is off");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusAudit", "NoFocusableLeaf", "A focused region rebound to a non-focusable widget reports NoFocusedLeaf",
                        _ =>
                        {
                            using (TuiApplication app = TwoRegions(new ListView<string>(), new ListView<string>()))
                            {
                                app.Bind("a", new Label(Text.From("static")));
                                FocusAuditResult result = FocusAudit.Run(app);
                                Check.True(Count(result, FocusAuditProblemKind.NoFocusedLeaf) > 0, "missing leaf reported");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusAudit", "StopLimit", "A limit smaller than the ring reports TraversalDidNotCycle",
                        _ =>
                        {
                            using (TuiApplication app = Framed(true))
                            {
                                FocusAuditResult result = FocusAudit.Run(app, new FocusAuditOptions { MaxStops = 2 });
                                Check.Equal(1, Count(result, FocusAuditProblemKind.TraversalDidNotCycle), "did not cycle within two stops");
                                Check.Equal(2, result.Stops, "two stops visited");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusAudit", "Guards", "Null apps, bad limits, unstarted apps, and open modals are rejected",
                        _ =>
                        {
                            Check.Throws<ArgumentNullException>(() => FocusAudit.Run(null!), "null app");
                            FocusAuditOptions options = new FocusAuditOptions();
                            Check.Throws<ArgumentOutOfRangeException>(() => options.MaxStops = 0, "limit below 1");
                            Check.Throws<ArgumentOutOfRangeException>(() => options.MaxStops = 10001, "limit above 10000");
                            Check.Throws<ArgumentNullException>(() => new FocusAuditProblem(0, FocusAuditProblemKind.LayoutShifted, null!, "x"), "null path");

                            using (TuiApplication app = new TuiApplication(new HeadlessBackend(20, 5)))
                                Check.Throws<InvalidOperationException>(() => FocusAudit.Run(app), "not started");

                            using (TuiApplication app = Framed(true))
                            {
                                app.ShowAsync(new MessageModal("Hello", "World", new[] { "OK" }));
                                Check.Throws<InvalidOperationException>(() => FocusAudit.Run(app), "modal open");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        })
                });
        }

        private static TuiApplication Framed(bool highlight)
        {
            TuiApplication app = new TuiApplication(new HeadlessBackend(60, 10));
            app.HighlightFocusedRegion = highlight;
            app.Layout = Layout.Create()
                .Add("list", r => r.ProportionalWidth(0, 0.3).FillHeight().WithPadding(0).WithBorder(BorderStyle.Line, "List"))
                .Add("split", r => r.ProportionalWidth(0.3, 0.4).FillHeight().WithPadding(0).WithBorder(BorderStyle.Line, "Split"))
                .Add("field", r => r.ProportionalWidth(0.7, 0.3).FillHeight().WithPadding(0).WithBorder(BorderStyle.Line, "Field"))
                .Build();
            app.Bind("list", new ListView<string>());
            app.Bind("split", new SplitView(SplitOrientation.Horizontal, new ListView<string>(), new ListView<string>()) { ForwardKeys = true });
            app.Bind("field", new TextField());
            app.Start();
            return app;
        }

        private static TuiApplication TwoRegions(IWidget a, IWidget b)
        {
            TuiApplication app = new TuiApplication(new HeadlessBackend(40, 8));
            app.HighlightFocusedRegion = true;
            app.Layout = Layout.Create()
                .Add("a", r => r.ProportionalWidth(0, 0.5).FillHeight().WithPadding(0).WithBorder(BorderStyle.Line))
                .Add("b", r => r.ProportionalWidth(0.5, 0.5).FillHeight().WithPadding(0).WithBorder(BorderStyle.Line))
                .Build();
            app.Bind("a", a);
            app.Bind("b", b);
            app.Start();
            return app;
        }

        private static int Count(FocusAuditResult result, FocusAuditProblemKind kind)
        {
            int count = 0;
            foreach (FocusAuditProblem problem in result.Problems)
            {
                if (problem.Kind == kind)
                    count++;
            }

            return count;
        }
    }
}

namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Hosting;
    using TUIKit.Input;
    using TUIKit.Layout;
    using TUIKit.Terminal;
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for focus repair, non-stop children, and inactive scopes (1.5.0, B3):
    /// <see cref="FocusScope.RepairFocus"/>, <see cref="FocusScope.AutoRepair"/>, <see cref="IFocusStop"/>,
    /// <see cref="FocusScope.IsTabStop"/>, the <see cref="FocusScope(bool)"/> constructor, and
    /// <see cref="TuiApplication.AutoRepairFocus"/>.
    /// </summary>
    public static class FocusRepairSuite
    {
        /// <summary>
        /// Builds the focus repair suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "FocusRepair",
                displayName: "Focus Repair, Non-Stop Children, and Inactive Scopes",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("FocusRepair", "HiddenChildMovesFocus", "Hiding the focused child moves focus to the next, then the previous, then out of the scope",
                        _ =>
                        {
                            FocusScope scope = new FocusScope();
                            ToggleLeaf a = scope.Add(new ToggleLeaf("a"));
                            ToggleLeaf b = scope.Add(new ToggleLeaf("b"));
                            ToggleLeaf c = scope.Add(new ToggleLeaf("c"));
                            List<IFocusable?> moves = new List<IFocusable?>();
                            scope.FocusMoved += moved => moves.Add(moved);
                            scope.SetFocus(b);
                            moves.Clear();

                            b.IsVisible = false;
                            Check.True(scope.RepairFocus(), "repaired");
                            Check.True(ReferenceEquals(c, scope.FocusedChild), "moved forward to c");
                            Check.Equal(1, moves.Count, "FocusMoved once");
                            Check.True(c.HasFocus && !b.HasFocus, "c told it has focus, b told it lost it");

                            c.IsEnabled = false;
                            Check.True(scope.RepairFocus(), "repaired again");
                            Check.True(ReferenceEquals(a, scope.FocusedChild), "c was last, so focus moved back to a");

                            a.IsVisible = false;
                            moves.Clear();
                            Check.True(scope.RepairFocus(), "repaired to nothing");
                            Check.Equal(-1, scope.FocusedIndex, "focus left the scope");
                            Check.True(scope.FocusedChild == null, "no focused child");
                            Check.Equal(1, moves.Count, "FocusMoved once");
                            Check.True(moves[0] == null, "FocusMoved(null)");
                            Check.False(FocusScope.IsFocusable(scope), "an emptied scope is not focusable");
                            Check.False(scope.RepairFocus(), "nothing focused: no repair");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusRepair", "NestedScopeRepairsParent", "When an inner scope empties, the parent's repair moves to its next child",
                        _ =>
                        {
                            FocusScope outer = new FocusScope();
                            FocusScope inner = outer.Add(new FocusScope());
                            ToggleLeaf only = inner.Add(new ToggleLeaf("only"));
                            ToggleLeaf after = outer.Add(new ToggleLeaf("after"));
                            Check.True(ReferenceEquals(inner, outer.FocusedChild), "inner starts focused");

                            only.IsVisible = false;
                            Check.True(inner.RepairFocus(), "inner emptied");
                            Check.True(outer.RepairFocus(), "outer moved on");
                            Check.True(ReferenceEquals(after, outer.FocusedChild), "parent moved to its next child");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusRepair", "NonStopChildren", "An IsFocusStop = false child is skipped by Tab, stays enabled, and keeps focus set by SetFocus",
                        _ =>
                        {
                            FocusScope scope = new FocusScope { Wrap = true };
                            ToggleLeaf a = scope.Add(new ToggleLeaf("a"));
                            ToggleLeaf chip = scope.Add(new ToggleLeaf("chip") { IsFocusStop = false });
                            ToggleLeaf c = scope.Add(new ToggleLeaf("c"));

                            scope.HandleKey(KeyEvent.Special(KeyCode.Tab));
                            Check.True(ReferenceEquals(c, scope.FocusedChild), "Tab skipped the chip");
                            scope.HandleKey(KeyEvent.Special(KeyCode.Tab, KeyModifiers.Shift));
                            Check.True(ReferenceEquals(a, scope.FocusedChild), "Shift+Tab skipped it too");
                            Check.True(chip.IsEnabled, "not disabled");
                            Check.True(FocusScope.IsFocusable(chip), "still focusable");
                            Check.False(FocusScope.IsTabStop(chip), "but not a tab stop");

                            scope.SetFocus(chip);
                            Check.True(chip.HasFocus, "SetFocus focused it");
                            Check.False(scope.RepairFocus(), "repair leaves a focusable non-stop child alone");
                            Check.True(ReferenceEquals(chip, scope.FocusedChild), "still on the chip");

                            FocusScope chipsOnly = new FocusScope();
                            chipsOnly.Add(new ToggleLeaf("x") { IsFocusStop = false });
                            Check.True(FocusScope.IsFocusable(chipsOnly), "a scope of non-stops can hold focus");
                            Check.False(FocusScope.IsTabStop(chipsOnly), "but Tab does not stop on it");
                            Check.Equal(-1, chipsOnly.FocusedIndex, "Add does not focus a non-stop child");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusRepair", "HostRepairsKeepsSetFocus", "SetFocus on a non-stop child survives a rendered frame with host repair on",
                        _ =>
                        {
                            FocusScope scope = new FocusScope();
                            ToggleLeaf a = scope.Add(new ToggleLeaf("a"));
                            ToggleLeaf chip = scope.Add(new ToggleLeaf("chip") { IsFocusStop = false });
                            using (TuiApplication app = SingleRegion(new HeadlessBackend(30, 6), new PathScopeWidget(scope)))
                            {
                                app.Start();
                                scope.SetFocus(chip);
                                app.RenderOnce();
                                Check.True(ReferenceEquals(chip, scope.FocusedChild), "still on the chip after a frame");
                                Check.True(ReferenceEquals(chip, app.CurrentFocusPath.Leaf), "focus path ends at the chip");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusRepair", "HostRepairsAfterFrame", "Hiding the focused child and rendering one frame repairs focus and the focus path, raising FocusPathChanged once",
                        _ =>
                        {
                            FocusScope scope = new FocusScope();
                            ToggleLeaf a = scope.Add(new ToggleLeaf("a"));
                            ToggleLeaf b = scope.Add(new ToggleLeaf("b"));
                            using (TuiApplication app = SingleRegion(new HeadlessBackend(30, 6), new PathScopeWidget(scope)))
                            {
                                app.Start();
                                app.RenderOnce();
                                Check.True(ReferenceEquals(a, app.CurrentFocusPath.Leaf), "starts on a");
                                int changes = 0;
                                app.FocusPathChanged += path => changes++;

                                a.IsVisible = false;
                                app.RenderOnce();
                                Check.True(ReferenceEquals(b, scope.FocusedChild), "repaired to b");
                                Check.True(ReferenceEquals(b, app.CurrentFocusPath.Leaf), "focus path is fresh");
                                Check.Equal(1, changes, "FocusPathChanged once");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusRepair", "HostMovesOffEmptyRegion", "When a region's scope empties, the host moves focus to the next region, and Tab skips the empty one",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 6);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                FocusScope scope = new FocusScope();
                                ToggleLeaf only = scope.Add(new ToggleLeaf("only"));
                                ToggleLeaf other = new ToggleLeaf("other");
                                app.Layout = Layout.Create()
                                    .Add("left", r => r.ProportionalWidth(0, 0.5).FillHeight())
                                    .Add("right", r => r.ProportionalWidth(0.5, 0.5).FillHeight())
                                    .Build();
                                app.Bind("left", new PathScopeWidget(scope));
                                app.Bind("right", other);
                                app.Start();
                                app.RenderOnce();
                                Check.Equal("left", app.FocusedRegion, "starts on the left");

                                only.IsVisible = false;
                                app.RenderOnce();
                                Check.Equal("right", app.FocusedRegion, "the host moved on");
                                Check.True(other.HasFocus, "the right region's widget has focus");
                                app.FocusNext();
                                Check.Equal("right", app.FocusedRegion, "Tab does not land on the emptied scope");
                                app.Stop();
                            }

                            HeadlessBackend hiddenBackend = new HeadlessBackend(40, 6);
                            using (TuiApplication hidden = new TuiApplication(hiddenBackend))
                            {
                                ToggleLeaf gone = new ToggleLeaf("gone");
                                hidden.Layout = Layout.Create()
                                    .Add("left", r => r.ProportionalWidth(0, 0.5).FillHeight())
                                    .Add("right", r => r.ProportionalWidth(0.5, 0.5).FillHeight())
                                    .Build();
                                hidden.Bind("left", gone);
                                hidden.Bind("right", new ToggleLeaf("shown"));
                                hidden.Start();
                                hidden.RenderOnce();
                                Check.Equal("left", hidden.FocusedRegion, "starts on the left");
                                gone.IsVisible = false;
                                hidden.RenderOnce();
                                Check.Equal("right", hidden.FocusedRegion, "a hidden region widget loses focus to the next region");
                                hidden.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusRepair", "InactiveScopeIsSilent", "A scope built with startsActive false sends no OnFocusChanged until it is entered",
                        _ =>
                        {
                            FocusScope scope = new FocusScope(false);
                            ToggleLeaf a = scope.Add(new ToggleLeaf("a"));
                            ToggleLeaf b = scope.Add(new ToggleLeaf("b"));
                            Check.Equal(0, a.FocusCalls.Count, "no call on Add for the first child");
                            Check.Equal(0, b.FocusCalls.Count, "no call on Add for the second child");
                            Check.True(ReferenceEquals(a, scope.FocusedChild), "a is still the focused child");
                            Check.False(scope.IsActive, "inactive");

                            scope.OnFocusChanged(true);
                            Check.Equal(1, a.FocusCalls.Count, "entering notifies once");
                            Check.True(a.HasFocus, "a now shows focus");

                            FocusScope active = new FocusScope();
                            ToggleLeaf first = active.Add(new ToggleLeaf("first"));
                            Check.True(first.HasFocus, "the parameterless constructor keeps the 1.4.0 Add notification");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FocusRepair", "OptOutAndGuards", "AutoRepair false keeps 1.4.0 behavior, and repair on an empty scope is a no-op",
                        _ =>
                        {
                            FocusScope scope = new FocusScope { AutoRepair = false };
                            ToggleLeaf a = scope.Add(new ToggleLeaf("a"));
                            scope.Add(new ToggleLeaf("b"));
                            using (TuiApplication app = SingleRegion(new HeadlessBackend(30, 6), new PathScopeWidget(scope)))
                            {
                                app.Start();
                                a.IsVisible = false;
                                app.RenderOnce();
                                Check.True(ReferenceEquals(a, scope.FocusedChild), "focus stays on the hidden child, as in 1.4.0");
                                app.Stop();
                            }

                            Check.False(new FocusScope().RepairFocus(), "empty scope: false, no throw");
                            Check.False(FocusScope.IsTabStop(null), "null is not a tab stop");
                            using (TuiApplication fresh = new TuiApplication(new HeadlessBackend()))
                                Check.True(fresh.AutoRepairFocus, "host repair is on by default");
                            return Task.CompletedTask;
                        })
                });
        }

        private static TuiApplication SingleRegion(HeadlessBackend backend, IWidget widget)
        {
            TuiApplication app = new TuiApplication(backend);
            app.Layout = Layout.Create().Add("main", r => r.FillWidth().FillHeight()).Build();
            app.Bind("main", widget);
            return app;
        }
    }
}

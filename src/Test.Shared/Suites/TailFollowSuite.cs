namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Input;
    using TUIKit.Testing;
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for tail-follow scrolling: the <see cref="TailFollow"/> state machine and its modes, the
    /// <see cref="Pane"/> scroll lock and "N new below" indicator built on it, and opt-in following for
    /// <see cref="ListView{T}"/>, including the rule that selection alone never stops following.
    /// </summary>
    public static class TailFollowSuite
    {
        /// <summary>
        /// Builds the tail follow suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "TailFollow",
                displayName: "Tail-Follow Scrolling",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("TailFollow", "StateMachine", "Viewport moves detach and re-attach; appends count while detached",
                        _ =>
                        {
                            TailFollow follow = new TailFollow();
                            List<bool> changes = new List<bool>();
                            follow.FollowingChanged += value => changes.Add(value);
                            Check.True(follow.IsFollowing, "starts following");
                            Check.True(follow.OnContentAppended(3), "append while following asks to scroll");

                            follow.OnViewportMoved(4, 10);
                            Check.False(follow.IsFollowing, "moving above the bottom detaches");
                            Check.False(follow.OnContentAppended(2), "append while detached does not scroll");
                            Check.Equal(2, follow.NewItemsBelow, "counted");
                            Check.Equal(" 2 new below ", follow.IndicatorText, "indicator text");

                            follow.OnViewportMoved(10, 10);
                            Check.True(follow.IsFollowing, "reaching the bottom re-attaches");
                            Check.Equal(0, follow.NewItemsBelow, "counter cleared");
                            Check.True(follow.IndicatorText == null, "no indicator while following");

                            follow.OnViewportMoved(0, 10);
                            follow.OnContentAppended(1);
                            follow.ReturnToTail();
                            Check.True(follow.IsFollowing && follow.NewItemsBelow == 0, "ReturnToTail re-attaches and clears");
                            Check.Equal(4, changes.Count, "one event per change: " + string.Join(",", changes));
                            Check.True(follow.OnContentAppended(0), "zero appended changes nothing");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("TailFollow", "Modes", "AlwaysFollow ignores scrolling up; Never never follows but still counts",
                        _ =>
                        {
                            TailFollow always = new TailFollow { Mode = TailFollowMode.AlwaysFollow };
                            always.OnViewportMoved(0, 10);
                            Check.True(always.IsFollowing, "AlwaysFollow stays attached");
                            Check.True(always.OnContentAppended(1), "and keeps scrolling");

                            TailFollow never = new TailFollow { Mode = TailFollowMode.Never };
                            Check.False(never.IsFollowing, "Never starts detached");
                            Check.False(never.OnContentAppended(4), "Never does not scroll");
                            Check.Equal(4, never.NewItemsBelow, "Never still counts");
                            never.ReturnToTail();
                            Check.False(never.IsFollowing, "ReturnToTail does not resume in Never mode");
                            Check.Equal(0, never.NewItemsBelow, "but clears the counter");
                            never.OnViewportMoved(10, 10);
                            Check.False(never.IsFollowing, "reaching the bottom does not resume in Never mode");

                            never.Mode = TailFollowMode.FollowAtBottom;
                            never.OnViewportMoved(10, 10);
                            Check.True(never.IsFollowing, "switching back to FollowAtBottom resumes at the bottom");

                            TailFollow quiet = new TailFollow { ShowIndicator = false };
                            quiet.OnViewportMoved(0, 5);
                            quiet.OnContentAppended(2);
                            Check.True(quiet.IndicatorText == null, "indicator hidden when turned off");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("TailFollow", "PaneScrollLockAndIndicator", "A pane follows at the bottom, holds still when scrolled up, counts, and returns",
                        _ =>
                        {
                            Pane pane = new Pane("log");
                            for (int i = 0; i < 20; i++)
                                pane.WriteLine("line " + i);

                            WidgetTester tester = WidgetTester.For(pane, 30, 5).Render();
                            Check.Equal("line 19", tester.Row(4), "last line visible while following");

                            pane.ScrollUp(3);
                            tester.Render();
                            string top = tester.Row(0);
                            Check.False(pane.IsAtBottom, "scrolled up detaches");

                            pane.WriteLine("line 20");
                            pane.WriteLine("line 21");
                            tester.Render();
                            Check.Equal(top, tester.Row(0), "viewport held still while new lines arrived");
                            Check.Equal(2, pane.NewSinceDetached, "two new lines counted");
                            Check.True(tester.Row(4).Contains("2 new below"), "indicator on the last row");
                            Check.True((tester.CellAt(29, 4).Style.Attributes & CellAttributes.Reverse) != 0, "indicator drawn in reverse video");

                            pane.ScrollToBottom();
                            tester.Render();
                            Check.True(pane.IsAtBottom, "re-attached");
                            Check.Equal("line 21", tester.Row(4), "newest line visible after returning");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("TailFollow", "PaneIndicatorClick", "Clicking the pane's indicator returns to the bottom",
                        _ =>
                        {
                            Pane pane = new Pane("log");
                            for (int i = 0; i < 20; i++)
                                pane.WriteLine("line " + i);

                            WidgetTester tester = WidgetTester.For(pane, 30, 5).Render();
                            pane.ScrollUp(5);
                            pane.WriteLine("new");
                            tester.Render();
                            tester.Click(28, 4);
                            Check.True(tester.LastMouseHandled, "indicator click consumed");
                            Check.True(pane.IsAtBottom, "click re-attached the pane");
                            tester.Render();
                            Check.Equal("new", tester.Row(4), "newest line visible");

                            tester.Click(0, 0);
                            Check.False(tester.LastMouseHandled, "a click elsewhere is not consumed");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("TailFollow", "PaneAlwaysFollowAndClear", "A pane in AlwaysFollow ignores scrolling up; Clear resets following",
                        _ =>
                        {
                            Pane pane = new Pane("log");
                            pane.TailFollow.Mode = TailFollowMode.AlwaysFollow;
                            for (int i = 0; i < 20; i++)
                                pane.WriteLine("line " + i);

                            WidgetTester tester = WidgetTester.For(pane, 30, 5).Render();
                            pane.ScrollUp(5);
                            tester.Render();
                            Check.Equal("line 19", tester.Row(4), "still pinned to the bottom");

                            Pane other = new Pane("other");
                            for (int i = 0; i < 20; i++)
                                other.WriteLine("x" + i);
                            WidgetTester.For(other, 30, 5).Render();
                            other.ScrollUp(5);
                            other.Clear();
                            Check.True(other.IsAtBottom, "Clear re-attaches");
                            Check.Equal(0, other.NewSinceDetached, "Clear clears the counter");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("TailFollow", "ListSelectionDoesNotDetach", "Selecting a visible row keeps a following list following, and appends still show",
                        _ =>
                        {
                            ListView<string> list = new ListView<string> { TailFollow = new TailFollow() };
                            for (int i = 0; i < 20; i++)
                                list.Append("item " + i);

                            WidgetTester tester = WidgetTester.For(list, 30, 5).Render();
                            Check.Equal("item 19", tester.Row(4), "last item visible");

                            list.Select(17);
                            tester.Render();
                            Check.True(list.TailFollow!.IsFollowing, "selecting a visible row does not detach");
                            Check.Equal(17, list.SelectedIndex, "selection applied");

                            list.Append("item 20");
                            tester.Render();
                            Check.Equal("item 20", tester.Row(4), "append after a selection change still follows");
                            Check.Equal(17, list.SelectedIndex, "append keeps the selection");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("TailFollow", "ListScrollUpDetachesEndReturns", "Moving the selection above the viewport detaches; End returns",
                        _ =>
                        {
                            ListView<string> list = new ListView<string> { TailFollow = new TailFollow() };
                            list.AppendRange(Items(20));
                            WidgetTester tester = WidgetTester.For(list, 30, 5).Render();

                            list.Select(5);
                            tester.Render();
                            Check.False(list.TailFollow!.IsFollowing, "scrolling up to the selection detached");
                            string top = tester.Row(0);

                            list.AppendRange(Items(3));
                            tester.Render();
                            Check.Equal(top, tester.Row(0), "viewport held still");
                            Check.True(tester.Row(4).Contains("3 new below"), "indicator shows the count");

                            tester.Press(KeyCode.End).Render();
                            Check.True(list.TailFollow!.IsFollowing, "End re-attached");
                            Check.Equal("item 2", tester.Row(4), "last item visible after End");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("TailFollow", "ListIndicatorClick", "Clicking a list's indicator returns to the bottom without changing the selection",
                        _ =>
                        {
                            ListView<string> list = new ListView<string> { TailFollow = new TailFollow() };
                            list.AppendRange(Items(20));
                            WidgetTester tester = WidgetTester.For(list, 30, 5).Render();
                            list.Select(2);
                            tester.Render();
                            list.Append("latest");
                            tester.Render();

                            tester.Click(29, 4);
                            Check.True(list.TailFollow!.IsFollowing, "click re-attached");
                            Check.Equal(2, list.SelectedIndex, "selection unchanged");
                            tester.Render();
                            Check.Equal("latest", tester.Row(4), "newest item visible");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("TailFollow", "ListWithoutFollowUnchanged", "Without TailFollow, appending keeps the 1.3 behavior",
                        _ =>
                        {
                            ListView<string> list = new ListView<string>();
                            list.AppendRange(Items(20));
                            WidgetTester tester = WidgetTester.For(list, 30, 5).Render();
                            Check.Equal("item 0", tester.Row(0), "view stays on the selection at the top");
                            Check.Equal(0, list.SelectedIndex, "first item selected after the first append");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("TailFollow", "Guards", "Invalid formats, counts, offsets, and null ranges are rejected",
                        _ =>
                        {
                            TailFollow follow = new TailFollow();
                            Check.Throws<ArgumentNullException>(() => follow.IndicatorFormat = null!, "null format");
                            Check.Throws<ArgumentException>(() => follow.IndicatorFormat = "new below", "format without a placeholder");
                            Check.Throws<ArgumentException>(() => follow.IndicatorFormat = "{0} {", "malformed format");
                            Check.Throws<ArgumentOutOfRangeException>(() => follow.OnContentAppended(-1), "negative count");
                            Check.Throws<ArgumentOutOfRangeException>(() => follow.OnViewportMoved(-1, 0), "negative offset");
                            Check.Throws<ArgumentOutOfRangeException>(() => follow.OnViewportMoved(0, -1), "negative maximum");
                            Check.Throws<ArgumentNullException>(() => new ListView<string>().AppendRange(null!), "null range");

                            follow.IndicatorFormat = "+{0}";
                            follow.OnViewportMoved(0, 3);
                            follow.OnContentAppended(4);
                            Check.Equal("+4", follow.IndicatorText, "custom format applied");
                            return Task.CompletedTask;
                        })
                });
        }

        private static List<string> Items(int count)
        {
            List<string> items = new List<string>();
            for (int i = 0; i < count; i++)
                items.Add("item " + i);
            return items;
        }
    }
}

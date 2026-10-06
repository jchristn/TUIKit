namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Content;

    /// <summary>
    /// Coverage for tail-follow counts that shrink and the public jump detach (1.5.0, B11):
    /// <see cref="TailFollow.OnContentRemoved"/>, <see cref="TailFollow.OnJumpedAway"/>, and
    /// <see cref="PaneLineHandle.Remove"/> lowering a pane's "N new below" count.
    /// </summary>
    public static class TailFollowShrinkSuite
    {
        /// <summary>
        /// Builds the tail-follow shrink suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "TailFollowShrink",
                displayName: "Tail-Follow Counts That Shrink",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("TailFollowShrink", "PaneRemovalLowersCount", "Detach, write 5 lines, remove 3 through their handles, and the count is 2",
                        _ =>
                        {
                            Pane pane = new Pane("log");
                            List<PaneLineHandle> old = new List<PaneLineHandle>();
                            for (int i = 0; i < 10; i++)
                                old.Add(pane.WriteLine(Text.From("old " + i)));
                            Render(pane);
                            pane.ScrollUp(4);
                            Check.False(pane.TailFollow.IsFollowing, "detached");

                            List<PaneLineHandle> fresh = new List<PaneLineHandle>();
                            for (int i = 0; i < 5; i++)
                                fresh.Add(pane.WriteLine(Text.From("new " + i)));
                            Check.Equal(5, pane.TailFollow.NewItemsBelow, "five new below");

                            fresh[0].Remove();
                            fresh[2].Remove();
                            fresh[4].Remove();
                            Check.Equal(2, pane.TailFollow.NewItemsBelow, "two left");

                            old[9].Remove();
                            Check.Equal(2, pane.TailFollow.NewItemsBelow, "removing a line the reader already had does not change the count");
                            fresh[1].Update("changed");
                            Check.Equal(2, pane.TailFollow.NewItemsBelow, "an update is not a removal");

                            fresh[1].Remove();
                            fresh[3].Remove();
                            Check.Equal(0, pane.TailFollow.NewItemsBelow, "all removed");
                            Check.True(pane.TailFollow.IndicatorText == null, "indicator gone");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("TailFollowShrink", "CountNeverNegative", "OnContentRemoved never lowers the count below zero, and a fresh detach starts a fresh count",
                        _ =>
                        {
                            TailFollow follow = new TailFollow();
                            follow.OnViewportMoved(0, 10);
                            follow.OnContentAppended(3);
                            follow.OnContentRemoved(10);
                            Check.Equal(0, follow.NewItemsBelow, "clamped at zero");
                            follow.OnContentRemoved(0);
                            Check.Equal(0, follow.NewItemsBelow, "zero changes nothing");

                            Pane pane = new Pane("p");
                            for (int i = 0; i < 10; i++)
                                pane.WriteLine(Text.From("line " + i));
                            Render(pane);
                            pane.ScrollUp(3);
                            PaneLineHandle first = pane.WriteLine(Text.From("a"));
                            pane.ScrollToBottom();
                            pane.ScrollUp(3);
                            PaneLineHandle second = pane.WriteLine(Text.From("b"));
                            Check.Equal(1, pane.TailFollow.NewItemsBelow, "one new since the second detach");
                            first.Remove();
                            Check.Equal(1, pane.TailFollow.NewItemsBelow, "a line from before the reset is not counted");
                            second.Remove();
                            Check.Equal(0, pane.TailFollow.NewItemsBelow, "the counted line lowers it");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("TailFollowShrink", "JumpedAwayDetaches", "OnJumpedAway detaches and clears the count, and End re-attaches",
                        _ =>
                        {
                            TailFollow follow = new TailFollow();
                            List<bool> changes = new List<bool>();
                            follow.FollowingChanged += value => changes.Add(value);
                            follow.OnJumpedAway();
                            Check.False(follow.IsFollowing, "detached");
                            Check.Equal(1, changes.Count, "FollowingChanged raised");
                            follow.OnContentAppended(2);
                            follow.OnJumpedAway();
                            Check.Equal(0, follow.NewItemsBelow, "count cleared");
                            follow.ReturnToTail();
                            Check.True(follow.IsFollowing, "End re-attached");

                            TailFollow always = new TailFollow { Mode = TailFollowMode.AlwaysFollow };
                            always.OnJumpedAway();
                            Check.True(always.IsFollowing, "AlwaysFollow keeps following");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("TailFollowShrink", "Guards", "A negative removed count is rejected",
                        _ =>
                        {
                            TailFollow follow = new TailFollow();
                            Check.Throws<ArgumentOutOfRangeException>(() => follow.OnContentRemoved(-1), "negative count");
                            return Task.CompletedTask;
                        })
                });
        }

        private static void Render(Pane pane)
        {
            pane.Render(new BufferSurface(new CellBuffer(20, 3)), CellStyle.Default);
        }
    }
}

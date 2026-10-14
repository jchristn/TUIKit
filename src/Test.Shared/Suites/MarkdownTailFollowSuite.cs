namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Input;
    using TUIKit.Testing;
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for 1.7 item V5: <see cref="MarkdownView.TailFollow"/> for a growing document.
    /// </summary>
    public static class MarkdownTailFollowSuite
    {
        private const string _Id = "MarkdownTailFollow";

        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: _Id,
                displayName: "Markdown Tail Follow",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(_Id, "NeverByDefault", "A new view uses Never and keeps its offset as the document grows",
                        _ =>
                        {
                            MarkdownView view = MarkdownViewKeysSuite.Long(30);
                            Check.Equal(TailFollowMode.Never, view.TailFollow.Mode, "default mode");
                            Snapshot.RenderWidget(view, 20, 5);
                            view.ScrollOffset = 3;
                            view.Markdown = MarkdownViewKeysSuite.Items(40);
                            string screen = Snapshot.RenderWidget(view, 20, 5);
                            Check.Equal(3, view.ScrollOffset, "offset kept");
                            Check.Equal(0, view.TailFollow.NewItemsBelow, "nothing counted");
                            Check.False(screen.Contains("new below"), "no indicator");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "AlwaysFollow", "AlwaysFollow stays at the end across growth",
                        _ =>
                        {
                            MarkdownView view = MarkdownViewKeysSuite.Long(3);
                            view.TailFollow.Mode = TailFollowMode.AlwaysFollow;
                            Snapshot.RenderWidget(view, 20, 5);
                            view.Markdown = MarkdownViewKeysSuite.Items(30);
                            string screen = Snapshot.RenderWidget(view, 20, 5);
                            Check.Equal(25, view.ScrollOffset, "at the end");
                            Check.True(screen.Contains("item 29"), "the last row is visible");
                            view.ScrollBy(-10);
                            view.Markdown = MarkdownViewKeysSuite.Items(32);
                            Snapshot.RenderWidget(view, 20, 5);
                            Check.Equal(27, view.ScrollOffset, "still at the end after scrolling up");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "FollowAtBottom", "FollowAtBottom detaches on scroll-up, counts new rows, and reattaches at the end",
                        _ =>
                        {
                            MarkdownView view = MarkdownViewKeysSuite.Long(3);
                            view.TailFollow.Mode = TailFollowMode.FollowAtBottom;
                            Snapshot.RenderWidget(view, 20, 5);
                            Check.True(view.TailFollow.IsFollowing, "attached while the document fits");
                            view.Markdown = MarkdownViewKeysSuite.Items(10);
                            Snapshot.RenderWidget(view, 20, 5);
                            Check.Equal(5, view.ScrollOffset, "followed the growth");

                            view.ScrollBy(-3);
                            Check.False(view.TailFollow.IsFollowing, "scrolling up detaches");
                            view.Markdown = MarkdownViewKeysSuite.Items(14);
                            string screen = Snapshot.RenderWidget(view, 20, 5);
                            Check.Equal(2, view.ScrollOffset, "the offset is kept while detached");
                            Check.Equal(4, view.TailFollow.NewItemsBelow, "new rows counted");
                            Check.True(screen.Contains("4 new below"), "the indicator shows on the last row");

                            view.Markdown = MarkdownViewKeysSuite.Items(12);
                            Snapshot.RenderWidget(view, 20, 5);
                            Check.Equal(2, view.TailFollow.NewItemsBelow, "a shrinking document lowers the count");

                            view.ScrollToEnd();
                            Check.True(view.TailFollow.IsFollowing, "reattached at the end");
                            Check.Equal(0, view.TailFollow.NewItemsBelow, "count cleared");
                            view.Markdown = MarkdownViewKeysSuite.Items(15);
                            Snapshot.RenderWidget(view, 20, 5);
                            Check.Equal(10, view.ScrollOffset, "follows again");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "ResizeIsNotContent", "A resize that changes the row count is not counted and keeps the offset clamped",
                        _ =>
                        {
                            MarkdownView view = new MarkdownView(MarkdownViewKeysSuite.Items(20), new MarkdownRenderOptions { MinWidth = 1 });
                            view.TailFollow.Mode = TailFollowMode.FollowAtBottom;
                            Snapshot.RenderWidget(view, 20, 5);
                            view.ScrollToTop();
                            view.ScrollBy(4);
                            Snapshot.RenderWidget(view, 6, 5);
                            Check.True(view.ContentHeight > 20, "narrower width wraps into more rows");
                            Check.Equal(0, view.TailFollow.NewItemsBelow, "not counted as new content");
                            Check.Equal(4, view.ScrollOffset, "same row number at the top");
                            Snapshot.RenderWidget(view, 40, 30);
                            Check.Equal(0, view.ScrollOffset, "clamped when everything fits");
                            Check.Equal(0, view.TailFollow.NewItemsBelow, "still nothing counted");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Indicator", "The indicator renders only when enabled, and a click on it returns to the end",
                        _ =>
                        {
                            MarkdownView view = MarkdownViewKeysSuite.Long(10);
                            view.TailFollow.Mode = TailFollowMode.FollowAtBottom;
                            Snapshot.RenderWidget(view, 20, 5);
                            view.ScrollToTop();
                            view.Markdown = MarkdownViewKeysSuite.Items(13);
                            view.TailFollow.ShowIndicator = false;
                            Check.False(Snapshot.RenderWidget(view, 20, 5).Contains("new below"), "hidden when disabled");
                            Check.Equal(3, view.TailFollow.NewItemsBelow, "still counted");
                            view.TailFollow.ShowIndicator = true;
                            Check.True(Snapshot.RenderWidget(view, 20, 5).Contains("3 new below"), "shown when enabled");

                            Check.False(view.HandleMouse(new MouseEvent(MouseEventKind.Press, MouseButton.Left, 0, 0, KeyModifiers.None, 1)), "a click elsewhere is not handled");
                            Check.True(view.HandleMouse(new MouseEvent(MouseEventKind.Press, MouseButton.Left, 19, 4, KeyModifiers.None, 1)), "a click on the indicator is handled");
                            Snapshot.RenderWidget(view, 20, 5);
                            Check.Equal(8, view.ScrollOffset, "back at the end");
                            Check.True(view.TailFollow.IsFollowing, "following again");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "SearchDetaches", "A search jump detaches the view from the end",
                        _ =>
                        {
                            MarkdownView view = MarkdownViewKeysSuite.Long(30);
                            view.TailFollow.Mode = TailFollowMode.FollowAtBottom;
                            view.ScrollToEnd();
                            Snapshot.RenderWidget(view, 20, 5);
                            Check.True(view.TailFollow.IsFollowing, "following at the end");
                            view.SetSearch("item 2");
                            Check.True(view.FindNext(), "found");
                            Check.False(view.TailFollow.IsFollowing, "the jump detached the view");
                            return Task.CompletedTask;
                        }),
                });
        }
    }
}

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
    /// Coverage for <see cref="MarkdownView"/> (plan item M8): rendering, measuring, scrolling, the wheel, option
    /// copies and invalidation, and argument guards.
    /// </summary>
    public static class MarkdownViewSuite
    {
        private const string _Id = "MarkdownView";

        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: _Id,
                displayName: "Markdown View",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(_Id, "Renders", "The view lays its Markdown out at the surface width",
                        _ =>
                        {
                            MarkdownView view = new MarkdownView("# Hi\n\n| a | b |\n|---|---|\n| 1 | 2 |");
                            string text = Snapshot.RenderWidget(view, 30, 10);
                            Check.True(text.Contains("Hi"), "heading");
                            Check.True(text.Contains("│ 1   │ 2   │"), "table");
                            Check.Equal(7, view.ContentHeight, "content height after render");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Measure", "Measure reports the row count, capped by the available height",
                        _ =>
                        {
                            MarkdownView view = new MarkdownView("- a\n- b\n- c");
                            Check.Equal(new Size(20, 3), view.Measure(new Size(20, 10)), "natural height");
                            Check.Equal(new Size(20, 2), view.Measure(new Size(20, 2)), "capped");
                            Check.Equal(new Size(0, 0), view.Measure(new Size(0, 5)), "no width");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Scroll", "ScrollOffset shows later rows and clamps past the end",
                        _ =>
                        {
                            MarkdownView view = new MarkdownView("- a\n- b\n- c\n- d");
                            view.ScrollOffset = 2;
                            string text = Snapshot.RenderWidget(view, 20, 2);
                            Check.True(text.Contains("• c") && text.Contains("• d"), "later rows");
                            Check.False(text.Contains("• a"), "earlier rows hidden");
                            view.ScrollOffset = 50;
                            Snapshot.RenderWidget(view, 20, 2);
                            Check.Equal(2, view.ScrollOffset, "clamped so the last rows stay visible");
                            Check.True(view.HandleMouse(new MouseEvent(MouseEventKind.Wheel, MouseButton.WheelUp, 0, 0, KeyModifiers.None, 0)), "wheel up handled");
                            Check.Equal(0, view.ScrollOffset, "scrolled up, floored at 0");
                            Check.True(view.HandleMouse(new MouseEvent(MouseEventKind.Wheel, MouseButton.WheelDown, 0, 0, KeyModifiers.None, 0)), "wheel down handled");
                            Check.Equal(2, view.ScrollOffset, "scrolled by WheelStep, clamped at once to the last full page");
                            Check.False(view.HandleMouse(new MouseEvent(MouseEventKind.Press, MouseButton.Left, 0, 0, KeyModifiers.None, 1)), "click not handled");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "OptionsCopied", "Options are copied on set; Invalidate picks up in-place edits",
                        _ =>
                        {
                            MarkdownRenderOptions o = new MarkdownRenderOptions();
                            MarkdownView view = new MarkdownView("| a |\n|---|", o);
                            o.Table.Glyphs = MarkdownTableGlyphs.Ascii;
                            Check.True(Snapshot.RenderWidget(view, 20, 4).Contains("┌"), "constructor copied");
                            view.Options = o;
                            Check.True(Snapshot.RenderWidget(view, 20, 4).Contains("+"), "setter applied");
                            view.Options.Table.Glyphs = MarkdownTableGlyphs.Rounded;
                            Check.True(Snapshot.RenderWidget(view, 20, 4).Contains("+"), "cached until invalidated");
                            view.Invalidate();
                            Check.True(Snapshot.RenderWidget(view, 20, 4).Contains("╭"), "invalidated");
                            view.Markdown = "plain";
                            Check.True(Snapshot.RenderWidget(view, 20, 4).Contains("plain"), "markdown setter invalidates");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Guards", "Null and out-of-range values throw",
                        _ =>
                        {
                            MarkdownView view = new MarkdownView();
                            Check.Throws<ArgumentNullException>(() => new MarkdownView(null!), "null markdown");
                            Check.Throws<ArgumentNullException>(() => new MarkdownView("x", null!), "null options");
                            Check.Throws<ArgumentNullException>(() => view.Markdown = null!, "null markdown setter");
                            Check.Throws<ArgumentNullException>(() => view.Options = null!, "null options setter");
                            Check.Throws<ArgumentOutOfRangeException>(() => view.ScrollOffset = -1, "negative offset");
                            Check.Throws<ArgumentOutOfRangeException>(() => view.WheelStep = 0, "wheel step 0");
                            Check.Throws<ArgumentOutOfRangeException>(() => view.WheelStep = 101, "wheel step 101");
                            Check.Throws<ArgumentOutOfRangeException>(() => view.RowsAt(0), "rows at 0");
                            Check.Throws<ArgumentNullException>(() => view.Render(null!), "null surface");
                            Check.Throws<ArgumentNullException>(() => view.HandleMouse(null!), "null mouse");
                            Check.False(view.TryGetFocusRect(out Rect _), "no focus rect");
                            Check.Equal(0, view.ContentHeight, "no content height before a render");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}

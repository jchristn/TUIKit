namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for focus-following scroll (<see cref="ScrollView"/> + <see cref="IScrollExtent"/>) and
    /// the runtime rebuild of a <see cref="Form"/>'s field set.
    /// </summary>
    public static class ScrollFormSuite
    {
        /// <summary>
        /// Builds the scroll/form suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "ScrollForm",
                displayName: "Scroll Focus & Dynamic Forms",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("ScrollForm", "FocusScrollsIntoView", "Focusing an off-screen field scrolls it into view",
                        _ =>
                        {
                            Form form = BuildForm(8);
                            ScrollView view = new ScrollView(form, 30, 40);

                            form.SetFocusedField(7);
                            Render(view, 20, 8);
                            Check.True(view.ScrollY > 0, "scrolled down to reveal the last field");

                            form.SetFocusedField(0);
                            Render(view, 20, 8);
                            Check.Equal(0, view.ScrollY, "scrolled back to the top for the first field");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ScrollForm", "NoAutoScrollWhenDisabled", "Disabling focus-follow leaves the scroll offset alone",
                        _ =>
                        {
                            Form form = BuildForm(8);
                            ScrollView view = new ScrollView(form, 30, 40);
                            view.AutoScrollToFocus = false;
                            view.ScrollTo(0, 5);
                            form.SetFocusedField(7);
                            Render(view, 20, 8);
                            Check.Equal(5, view.ScrollY, "offset unchanged when auto-scroll is off");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ScrollForm", "EnsureVisibleClamps", "EnsureVisible clamps rather than throwing",
                        _ =>
                        {
                            Form form = BuildForm(4);
                            ScrollView view = new ScrollView(form, 20, 30);
                            Render(view, 20, 6);
                            view.EnsureVisible(-100, -5); // must not throw
                            Check.True(view.ScrollY >= 0, "scroll stays non-negative");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ScrollForm", "RebuildFields", "Clear and re-add rebuilds the field set",
                        _ =>
                        {
                            Form form = new Form();
                            form.Add("A", new TextField());
                            form.Add("B", new TextField());
                            Check.Equal(2, form.FieldCount, "two fields");

                            form.Clear();
                            Check.Equal(0, form.FieldCount, "cleared");
                            form.Add("X", new TextField());
                            form.Add("Y", new TextField());
                            form.Add("Z", new TextField());
                            Check.Equal(3, form.FieldCount, "rebuilt with three fields");
                            Check.Equal(0, form.FocusedIndex, "focus reset to first field");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ScrollForm", "SetFocusedGuard", "SetFocusedField rejects an out-of-range index",
                        _ =>
                        {
                            Form form = new Form();
                            form.Add("A", new TextField());
                            Check.Throws<ArgumentOutOfRangeException>(() => form.SetFocusedField(5), "out of range");
                            return Task.CompletedTask;
                        })
                });
        }

        private static Form BuildForm(int fields)
        {
            Form form = new Form();
            for (int i = 0; i < fields; i++)
                form.Add("Field " + i, new TextField());

            return form;
        }

        private static void Render(IWidget widget, int width, int height)
        {
            CellBuffer buffer = new CellBuffer(width, height);
            widget.Render(new BufferSurface(buffer));
        }
    }
}

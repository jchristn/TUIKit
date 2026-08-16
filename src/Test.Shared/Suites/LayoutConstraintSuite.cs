namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Layout;
    using TUIKit.Widgets;

    /// <summary>
    /// Touchstone suite adding coverage for the layout DSL and constraint validation, plus more
    /// positive/negative cases for markup, the split-layout builder, tabs, trees, and the fuzzy list.
    /// </summary>
    public static class LayoutConstraintSuite
    {
        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "LayoutConstraint",
                displayName: "Layout DSL & Constraint Validation",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("LayoutConstraint", "ColumnLayout", "Column layout sizes fixed and fill slots",
                        _ =>
                        {
                            Layout layout = Layout.Column(
                                LayoutSlot.Fixed("top", 3),
                                LayoutSlot.Fill("body"),
                                LayoutSlot.Fixed("bottom", 2));

                            Size size = new Size(20, 10);
                            Rect top = layout.FindById("top")!.Resolve(size);
                            Rect body = layout.FindById("body")!.Resolve(size);
                            Rect bottom = layout.FindById("bottom")!.Resolve(size);

                            Check.Equal(3, top.Height, "top takes its fixed height");
                            Check.Equal(2, bottom.Height, "bottom takes its fixed height");
                            Check.Equal(5, body.Height, "fill takes the remaining height");
                            Check.Equal(20, body.Width, "fill spans the width");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("LayoutConstraint", "RowLayout", "Row layout splits fill slots equally",
                        _ =>
                        {
                            Layout layout = Layout.Row(LayoutSlot.Fill("a"), LayoutSlot.Fill("b"));
                            Size size = new Size(10, 4);
                            Rect a = layout.FindById("a")!.Resolve(size);
                            Rect b = layout.FindById("b")!.Resolve(size);
                            Check.Equal(4, a.Height, "fill spans height");
                            Check.True(a.Width >= 4 && b.Width >= 4, "two fills share the width");

                            Check.Throws<ArgumentException>(() => Layout.Column(), "empty column rejected");
                            Check.Throws<ArgumentException>(() => Layout.Row(null!), "null slots rejected");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("LayoutConstraint", "SlotGuards", "Layout slots and constraints validate arguments",
                        _ =>
                        {
                            Check.Throws<ArgumentException>(() => LayoutSlot.Fixed("", 3), "empty slot id");
                            Check.Throws<ArgumentOutOfRangeException>(() => LayoutSlot.Fixed("a", 0), "non-positive cells");
                            Check.Throws<ArgumentException>(() => LayoutSlot.Fill(null!), "null fill id");

                            Check.Throws<ArgumentOutOfRangeException>(() => AxisConstraint.Fixed(-1, 3), "negative offset");
                            Check.Throws<ArgumentOutOfRangeException>(() => AxisConstraint.Fixed(0, 0), "zero length");
                            Check.Throws<ArgumentOutOfRangeException>(() => AxisConstraint.FromEnd(0, -2), "negative end length");
                            Check.Throws<ArgumentOutOfRangeException>(() => AxisConstraint.FromEnd(-1, 3), "negative end offset");
                            Check.Throws<ArgumentOutOfRangeException>(() => AxisConstraint.Stretch(min: 0), "zero min");
                            Check.Throws<ArgumentOutOfRangeException>(() => AxisConstraint.Stretch(startPad: -1), "negative start pad");
                            Check.Throws<ArgumentOutOfRangeException>(() => AxisConstraint.Stretch(endPad: -1), "negative end pad");
                            Check.Throws<ArgumentOutOfRangeException>(() => AxisConstraint.Stretch(min: 5, max: 2), "max below min");
                            Check.Throws<ArgumentOutOfRangeException>(() => AxisConstraint.Proportional(0.0, 0.0), "zero length fraction");
                            Check.Throws<ArgumentOutOfRangeException>(() => AxisConstraint.Proportional(0.0, 1.5), "length fraction above one");
                            Check.Throws<ArgumentOutOfRangeException>(() => AxisConstraint.Proportional(0.0, 0.5, min: 5, max: 2), "proportional max below min");
                            Check.Throws<ArgumentOutOfRangeException>(() => AxisConstraint.Partition(0, 0, 3, 3), "index equals count");
                            Check.Throws<ArgumentOutOfRangeException>(() => AxisConstraint.Partition(0, 0, 0, 0), "zero count");
                            Check.Throws<ArgumentOutOfRangeException>(() => AxisConstraint.Partition(-1, 0, 0, 2), "negative before");
                            Check.Throws<ArgumentOutOfRangeException>(() => AxisConstraint.Partition(0, -1, 0, 2), "negative after");
                            Check.Throws<ArgumentOutOfRangeException>(() => AxisConstraint.Partition(0, 0, 0, 2, min: 0), "partition zero min");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("LayoutConstraint", "ConstraintResolve", "Axis constraints resolve to the expected offset and length",
                        _ =>
                        {
                            AxisConstraint.Stretch(startPad: 2, endPad: 1).Resolve(10, out int sOffset, out int sLength);
                            Check.Equal(2, sOffset, "stretch offset is the start pad");
                            Check.Equal(7, sLength, "stretch fills between the pads");

                            AxisConstraint.FromEnd(1, 3).Resolve(10, out int eOffset, out int eLength);
                            Check.Equal(3, eLength, "from-end keeps its fixed length");
                            Check.Equal(6, eOffset, "from-end anchors against the end edge");

                            AxisConstraint.Proportional(0.0, 0.5).Resolve(10, out int pOffset, out int pLength);
                            Check.Equal(0, pOffset, "proportional offset at origin");
                            Check.Equal(5, pLength, "proportional length is half the surface");

                            AxisConstraint.Partition(0, 0, 1, 2).Resolve(10, out int partOffset, out int partLength);
                            Check.Equal(5, partOffset, "second partition starts at the midpoint");
                            Check.Equal(5, partLength, "second partition takes its share");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("LayoutConstraint", "MarkupEscapes", "Markup escapes brackets and tolerates stray tags",
                        _ =>
                        {
                            Check.Equal("[x]", Markup.Parse("[[x]]").ToString(), "double brackets escape to literals");
                            Check.Equal("plain", Markup.Parse("plain").ToString(), "plain text passes through");
                            Check.Equal("bold text", Markup.Parse("[bold]bold text[/]").ToString(), "tags stripped from text");
                            Check.Equal("unclosed", Markup.Parse("[bold]unclosed").ToString(), "unclosed tag does not throw");
                            Check.Equal("extra", Markup.Parse("extra[/]").ToString(), "stray close tag ignored");

                            Check.Throws<ArgumentNullException>(() => Markup.Parse(null!), "null markup");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("LayoutConstraint", "TabsAndTree", "Tabs and tree validate indices and null arguments",
                        _ =>
                        {
                            TabView tabs = new TabView()
                                .Add("A", new Label(Text.From("a")))
                                .Add("B", new Label(Text.From("b")));
                            Check.Equal(0, tabs.ActiveIndex, "first tab active");
                            tabs.Activate(1);
                            Check.Equal(1, tabs.ActiveIndex, "activated second tab");
                            Check.Throws<ArgumentOutOfRangeException>(() => tabs.Activate(2), "activate out of range");
                            Check.Throws<ArgumentOutOfRangeException>(() => tabs.Activate(-1), "activate negative");
                            Check.Throws<ArgumentNullException>(() => tabs.Add(null!, new Label(Text.From("x"))), "null tab name");
                            Check.Throws<ArgumentNullException>(() => tabs.Add("x", null!), "null tab widget");

                            Check.Throws<ArgumentNullException>(() => new Tree<string>(null!, s => Array.Empty<string>(), s => s), "null root");
                            Check.Throws<ArgumentNullException>(() => new Tree<string>("r", null!, s => s), "null children selector");
                            Check.Throws<ArgumentNullException>(() => new Tree<string>("r", s => Array.Empty<string>(), null!), "null label selector");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("LayoutConstraint", "FuzzyAndBanner", "Fuzzy list rejects null query; banner handles edges",
                        _ =>
                        {
                            FuzzyList<string> list = new FuzzyList<string>(new[] { "one", "two", "three" });
                            list.Query = "t";
                            Check.Equal(2, list.MatchCount, "two match 't'");
                            list.Query = string.Empty;
                            Check.Equal(3, list.MatchCount, "empty query matches all");
                            Check.Throws<ArgumentNullException>(() => list.Query = null!, "null query rejected");

                            IReadOnlyList<string> blank = Banner.Render(string.Empty);
                            Check.Equal(5, blank.Count, "empty banner still five rows");
                            Check.Equal(0, blank[0].Length, "empty banner rows are empty");

                            IReadOnlyList<string> dots = Banner.Render("A", ".");
                            bool sawDot = false;
                            foreach (string row in dots)
                            {
                                if (row.Contains("."))
                                    sawDot = true;
                            }

                            Check.True(sawDot, "custom ink glyph used");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("LayoutConstraint", "DataTableBind", "Data table keeps selection valid after rebind",
                        _ =>
                        {
                            DataTable<string> table = new DataTable<string>().Column("V", s => s);
                            table.Bind(new List<string> { "a", "b", "c", "d" });
                            table.HandleKey(TUIKit.Input.KeyEvent.Special(TUIKit.Input.KeyCode.Down));
                            table.HandleKey(TUIKit.Input.KeyEvent.Special(TUIKit.Input.KeyCode.Down));
                            table.HandleKey(TUIKit.Input.KeyEvent.Special(TUIKit.Input.KeyCode.Down));
                            Check.Equal(3, table.SelectedIndex, "selection at last row");

                            table.Bind(new List<string> { "x", "y" });
                            Check.Equal(1, table.SelectedIndex, "selection clamped into the smaller set");
                            Check.Equal("y", table.SelectedRow, "selected row valid after rebind");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}

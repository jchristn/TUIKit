namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Layout;

    /// <summary>
    /// Touchstone suite covering the split/grid layout DSL (10.6): <see cref="Layout.Row"/> and
    /// <see cref="Layout.Column"/> with fixed and fill slots.
    /// </summary>
    public static class LayoutDslSuite
    {
        /// <summary>
        /// Builds the layout DSL suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "LayoutDsl",
                displayName: "Layout DSL",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("LayoutDsl", "ColumnHeaderBodyFooter", "Column stacks fixed header/footer around a fill",
                        _ =>
                        {
                            Layout layout = Layout.Column(
                                LayoutSlot.Fixed("header", 1),
                                LayoutSlot.Fill("body"),
                                LayoutSlot.Fixed("footer", 1));
                            Size size = new Size(80, 24);
                            Check.Equal(new Rect(0, 0, 80, 1), layout.FindById("header")!.Resolve(size), "Header");
                            Check.Equal(new Rect(0, 1, 80, 22), layout.FindById("body")!.Resolve(size), "Body fills the middle");
                            Check.Equal(new Rect(0, 23, 80, 1), layout.FindById("footer")!.Resolve(size), "Footer at the bottom");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("LayoutDsl", "RowSidebar", "Row places a fixed sidebar beside a fill",
                        _ =>
                        {
                            Layout layout = Layout.Row(LayoutSlot.Fixed("side", 20), LayoutSlot.Fill("main"));
                            Size size = new Size(80, 24);
                            Check.Equal(new Rect(0, 0, 20, 24), layout.FindById("side")!.Resolve(size), "Sidebar");
                            Check.Equal(new Rect(20, 0, 60, 24), layout.FindById("main")!.Resolve(size), "Main fills the rest");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("LayoutDsl", "TwoFills", "Two fill slots split the space equally",
                        _ =>
                        {
                            Layout layout = Layout.Column(LayoutSlot.Fill("a"), LayoutSlot.Fill("b"));
                            Size size = new Size(80, 10);
                            Check.Equal(new Rect(0, 0, 80, 5), layout.FindById("a")!.Resolve(size), "Top half");
                            Check.Equal(new Rect(0, 5, 80, 5), layout.FindById("b")!.Resolve(size), "Bottom half");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("LayoutDsl", "LeadingTrailingFixed", "A fill sits between fixed edges",
                        _ =>
                        {
                            Layout layout = Layout.Row(
                                LayoutSlot.Fixed("left", 10),
                                LayoutSlot.Fill("mid"),
                                LayoutSlot.Fixed("right", 10));
                            Size size = new Size(100, 24);
                            Check.Equal(new Rect(0, 0, 10, 24), layout.FindById("left")!.Resolve(size), "Left");
                            Check.Equal(new Rect(10, 0, 80, 24), layout.FindById("mid")!.Resolve(size), "Middle");
                            Check.Equal(new Rect(90, 0, 10, 24), layout.FindById("right")!.Resolve(size), "Right tracks the edge");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("LayoutDsl", "Guards", "Empty layouts and non-positive fixed sizes throw",
                        _ =>
                        {
                            Check.Throws<ArgumentException>(() => Layout.Column(), "Empty column");
                            Check.Throws<ArgumentException>(() => Layout.Row(), "Empty row");
                            Check.Throws<ArgumentOutOfRangeException>(() => LayoutSlot.Fixed("x", 0), "Zero cells");
                            Check.Throws<ArgumentException>(() => LayoutSlot.Fill(""), "Empty id");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}

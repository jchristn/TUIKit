namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Layout;

    /// <summary>
    /// Touchstone suite covering the developer-defined region layout: per-region resize behavior,
    /// minimum-size derivation, overlap detection, and the block-screen threshold.
    /// </summary>
    public static class LayoutSuite
    {
        /// <summary>
        /// Builds the layout suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Layout",
                displayName: "Region Layout",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Layout", "FixedStaysPut", "A fixed region does not scale with the surface",
                        _ =>
                        {
                            Region region = Region.Define("side").LeftAnchored(0, 20).FillHeight(0, 0).Build();
                            Check.Equal(new Rect(0, 0, 20, 24), region.Resolve(new Size(80, 24)), "At 80x24");
                            Check.Equal(new Rect(0, 0, 20, 50), region.Resolve(new Size(200, 50)), "Width unchanged at 200x50");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Layout", "RightAnchored", "A right-anchored region tracks the right edge",
                        _ =>
                        {
                            Region region = Region.Define("tools").RightAnchored(0, 30).FillHeight().Build();
                            Check.Equal(new Rect(50, 0, 30, 24), region.Resolve(new Size(80, 24)), "At 80 wide");
                            Check.Equal(new Rect(170, 0, 30, 24), region.Resolve(new Size(200, 24)), "At 200 wide");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Layout", "FillGrows", "A stretch region grows with the surface",
                        _ =>
                        {
                            Region region = Region.Define("main").FillWidth(20, 30).FillHeight(1, 1).Build();
                            Check.Equal(new Rect(20, 1, 30, 22), region.Resolve(new Size(80, 24)), "At 80x24");
                            Check.Equal(new Rect(20, 1, 150, 48), region.Resolve(new Size(200, 50)), "Grows to fill");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Layout", "Proportional", "A proportional region scales by fraction",
                        _ =>
                        {
                            Region region = Region.Define("half").ProportionalWidth(0.5, 0.5).FillHeight().Build();
                            Check.Equal(new Rect(40, 0, 40, 10), region.Resolve(new Size(80, 10)), "Right half at 80");
                            Check.Equal(new Rect(100, 0, 100, 10), region.Resolve(new Size(200, 10)), "Right half at 200");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Layout", "MinimumSize", "Layout minimum is the max of region needs",
                        _ =>
                        {
                            Layout layout = Layout.Create()
                                .Add("left", r => r.LeftAnchored(0, 24).FillHeight().WithPadding(0))
                                .Add("right", r => r.RightAnchored(0, 40).FillHeight().WithPadding(0))
                                .Add("footer", r => r.FillWidth().BottomAnchored(0, 3).WithPadding(0))
                                .Build();
                            // Each region's own minimum is independent. Horizontal min = max(24, 40, 1) = 40.
                            Check.Equal(40, layout.MinimumSize.Width, "Minimum width");
                            Check.True(layout.FitsIn(new Size(80, 24)), "80x24 fits");
                            Check.False(layout.FitsIn(new Size(30, 24)), "30 wide does not fit");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Layout", "OverlapDetection", "Overlapping regions are detected",
                        _ =>
                        {
                            Layout disjoint = Layout.Create()
                                .Add("a", r => r.LeftAnchored(0, 20).FillHeight())
                                .Add("b", r => r.RightAnchored(0, 20).FillHeight())
                                .Build();
                            Check.False(disjoint.HasOverlap(new Size(80, 24)), "Disjoint at 80 wide");

                            Layout overlapping = Layout.Create()
                                .Add("a", r => r.LeftAnchored(0, 50).FillHeight())
                                .Add("b", r => r.LeftAnchored(40, 30).FillHeight())
                                .Build();
                            Check.True(overlapping.HasOverlap(new Size(80, 24)), "Regions overlap at columns 40-49");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Layout", "Padding", "Region padding insets the content rectangle",
                        _ =>
                        {
                            // Padding defaults to one cell on every side.
                            Region def = Region.Define("d").FillWidth().FillHeight().Build();
                            Check.Equal(new Rect(0, 0, 40, 10), def.Resolve(new Size(40, 10)), "Resolve is the full rect");
                            Check.Equal(new Rect(1, 1, 38, 8), def.ContentRect(new Size(40, 10)), "Default padding insets one cell on every side");

                            // Explicit padding overrides the default.
                            Region none = Region.Define("n").FillWidth().FillHeight().WithPadding(0).Build();
                            Check.Equal(new Rect(0, 0, 40, 10), none.ContentRect(new Size(40, 10)), "WithPadding(0) removes padding");

                            Region hv = Region.Define("hv").FillWidth().FillHeight().WithPadding(2, 0, 3, 0).Build();
                            Check.Equal(new Rect(2, 0, 35, 10), hv.ContentRect(new Size(40, 10)), "Explicit left 2 / right 3, vertical 0");
                            Check.Equal(6, hv.MinimumSize.Width, "Minimum width includes horizontal padding");

                            Region bordered = Region.Define("box").FillWidth().FillHeight().WithPadding(1, 1, 1, 1).Build();
                            Check.Equal(new Rect(1, 1, 8, 8), bordered.ContentRect(new Size(10, 10)), "Uniform padding");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Layout", "Border", "Region border insets content and grows the minimum",
                        _ =>
                        {
                            Region plain = Region.Define("p").FillWidth().FillHeight().WithPadding(0).Build();
                            Check.False(plain.HasBorder, "No border by default");

                            Region bordered = Region.Define("b").FillWidth().FillHeight().WithPadding(0).WithBorder(BorderStyle.Rounded, "Panel").Build();
                            Check.True(bordered.HasBorder, "Border present");
                            Check.Equal(BorderStyle.Rounded, bordered.Border, "Border style stored");
                            Check.Equal("Panel", bordered.BorderTitle, "Border title stored");
                            // Border insets content one cell on every side (padding here is zero).
                            Check.Equal(new Rect(1, 1, 8, 8), bordered.ContentRect(new Size(10, 10)), "Content inset for the border");
                            Check.Equal(3, bordered.MinimumSize.Width, "Minimum width includes the border (1 + 2)");

                            // Border plus the default one-cell padding insets content by two.
                            Region both = Region.Define("bp").FillWidth().FillHeight().WithBorder(BorderStyle.Double).Build();
                            Check.Equal(new Rect(2, 2, 16, 16), both.ContentRect(new Size(20, 20)), "Border and padding both inset");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Layout", "BorderGlyphs", "Border styles render distinct corner glyphs",
                        _ =>
                        {
                            CellBuffer buffer = new CellBuffer(6, 3);
                            BufferSurface surface = new BufferSurface(buffer);
                            surface.DrawBox(new Rect(0, 0, 6, 3), CellStyle.Default, BorderStyle.Rounded, null);
                            Check.Equal("╭", buffer.Get(0, 0).Grapheme, "Rounded top-left");

                            surface.Fill(new Rect(0, 0, 6, 3), Cell.Empty);
                            surface.DrawBox(new Rect(0, 0, 6, 3), CellStyle.Default, BorderStyle.Double, null);
                            Check.Equal("╔", buffer.Get(0, 0).Grapheme, "Double top-left");

                            surface.Fill(new Rect(0, 0, 6, 3), Cell.Empty);
                            surface.DrawBox(new Rect(0, 0, 6, 3), CellStyle.Default, BorderStyle.Ascii, null);
                            Check.Equal("+", buffer.Get(0, 0).Grapheme, "ASCII top-left");

                            surface.Fill(new Rect(0, 0, 6, 3), Cell.Empty);
                            surface.DrawBox(new Rect(0, 0, 6, 3), CellStyle.Default, BorderStyle.None, null);
                            Check.Equal(" ", buffer.Get(0, 0).Grapheme, "None draws nothing");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Layout", "BlockScreen", "Block screen renders centered message",
                        _ =>
                        {
                            CellBuffer buffer = new CellBuffer(30, 6);
                            BufferSurface surface = new BufferSurface(buffer);
                            LayoutBlockScreen.Render(surface, new Size(80, 24), new Size(30, 6), CellStyle.Default);

                            string row = ReadRow(buffer, 2);
                            Check.True(row.Contains("Terminal too small"), "Message present on row 2");
                            return Task.CompletedTask;
                        })
                });
        }

        private static string ReadRow(CellBuffer buffer, int row)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            for (int x = 0; x < buffer.Width; x++)
                builder.Append(buffer.Get(x, row).Grapheme);

            return builder.ToString();
        }
    }
}

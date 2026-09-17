namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Widgets;

    /// <summary>
    /// Touchstone suite for the 0.13.0 distribution widgets: <see cref="BoxPlotChart"/>,
    /// <see cref="Histogram"/>, and <see cref="HeatMap"/>. Every case renders into a headless
    /// <see cref="BufferSurface"/> and asserts glyph placement, matching the style of
    /// <see cref="ChartsIconsColorSuite"/>.
    /// </summary>
    public static class DistributionChartsSuite
    {
        /// <summary>
        /// Builds the distribution-charts suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "DistributionCharts",
                displayName: "Distribution Charts",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("DistributionCharts", "BoxPlotScales", "Box position scales with value",
                        _ =>
                        {
                            BoxPlotChart chart = new BoxPlotChart();
                            chart.ShowAxis = false;
                            chart.Add("a", 0, 10, 20, 30, 100);
                            chart.Add("b", 0, 10, 20, 90, 100);

                            CellBuffer buffer = new CellBuffer(40, 2);
                            chart.Render(new BufferSurface(buffer));

                            int lowBox = LastGlyphCol(buffer, 0, "█");
                            int highBox = LastGlyphCol(buffer, 1, "█");
                            Check.True(lowBox > 0, "first row drew a box");
                            Check.True(highBox > lowBox, "a larger High pushes the box farther right");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("DistributionCharts", "BoxPlotMidMarker", "Mid marker sits between the box edges",
                        _ =>
                        {
                            BoxPlotChart chart = new BoxPlotChart();
                            chart.ShowAxis = false;
                            chart.Add("row", 0, 20, 50, 80, 100);

                            CellBuffer buffer = new CellBuffer(40, 1);
                            chart.Render(new BufferSurface(buffer));

                            int firstBox = FirstGlyphCol(buffer, 0, "█");
                            int lastBox = LastGlyphCol(buffer, 0, "█");
                            int mid = FirstGlyphCol(buffer, 0, "┃");
                            Check.True(firstBox >= 0 && mid >= 0, "box and mid marker drawn");
                            Check.True(mid >= firstBox && mid <= lastBox, "mid marker lies within the box span");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("DistributionCharts", "BoxPlotDegenerate", "All-equal summary renders one marker and no whiskers",
                        _ =>
                        {
                            BoxPlotChart chart = new BoxPlotChart();
                            chart.ShowAxis = false;
                            chart.Add("flat", 5, 5, 5, 5, 5);

                            CellBuffer buffer = new CellBuffer(20, 1);
                            chart.Render(new BufferSurface(buffer));

                            Check.Equal(1, CountGlyph(buffer, 0, "┃"), "exactly one mid marker");
                            Check.Equal(0, CountGlyph(buffer, 0, "─"), "no whiskers");
                            Check.Equal(0, CountGlyph(buffer, 0, "█"), "no box");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("DistributionCharts", "BoxPlotEmpty", "Empty chart renders nothing and measures to zero height",
                        _ =>
                        {
                            BoxPlotChart chart = new BoxPlotChart();
                            Check.Equal(0, chart.Measure(new Size(20, 10)).Height, "empty measures to height 0");

                            CellBuffer buffer = new CellBuffer(20, 4);
                            chart.Render(new BufferSurface(buffer));
                            Check.False(HasInk(buffer), "empty draws nothing");

                            Check.Throws<ArgumentNullException>(() => chart.Add(null!, 0, 1, 2, 3, 4), "null label rejected");
                            Check.Throws<ArgumentNullException>(() => chart.Add((BoxSummary)null!), "null summary rejected");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("DistributionCharts", "BoxPlotClamps", "Non-finite inputs clamp instead of throwing",
                        _ =>
                        {
                            BoxPlotChart chart = new BoxPlotChart();
                            chart.ShowAxis = false;
                            chart.Add("bad", double.NegativeInfinity, -50, double.NaN, 50, double.PositiveInfinity);
                            chart.SetRange(0, 100);

                            CellBuffer buffer = new CellBuffer(40, 1);
                            chart.Render(new BufferSurface(buffer)); // must not throw
                            Check.True(HasInk(buffer), "a bad sample still renders a degraded row");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("DistributionCharts", "BoxPlotSetRange", "A fixed range changes the rendered layout",
                        _ =>
                        {
                            BoxPlotChart derived = new BoxPlotChart();
                            derived.ShowAxis = false;
                            derived.Add("a", 0, 10, 20, 30, 40);

                            BoxPlotChart fixedRange = new BoxPlotChart();
                            fixedRange.ShowAxis = false;
                            fixedRange.Add("a", 0, 10, 20, 30, 40);
                            fixedRange.SetRange(0, 400);

                            CellBuffer left = new CellBuffer(40, 1);
                            CellBuffer right = new CellBuffer(40, 1);
                            derived.Render(new BufferSurface(left));
                            fixedRange.Render(new BufferSurface(right));

                            int derivedEnd = LastGlyphCol(left, 0, "─");
                            int fixedEnd = LastGlyphCol(right, 0, "─");
                            Check.True(derivedEnd > fixedEnd, "the wider fixed range compresses the same data to the left");

                            Check.Throws<ArgumentException>(() => fixedRange.SetRange(10, 10), "empty range rejected");
                            Check.Throws<ArgumentException>(() => fixedRange.SetRange(double.NaN, 1), "non-finite range rejected");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("DistributionCharts", "BoxPlotLabelAlign", "Label column aligns across rows of differing label length",
                        _ =>
                        {
                            BoxPlotChart chart = new BoxPlotChart();
                            chart.ShowAxis = false;
                            chart.Add("a", 0, 10, 20, 30, 40);
                            chart.Add("longer", 0, 10, 20, 30, 40);

                            CellBuffer buffer = new CellBuffer(40, 2);
                            chart.Render(new BufferSurface(buffer));

                            int startA = FirstGlyphCol(buffer, 0, "─");
                            int startB = FirstGlyphCol(buffer, 1, "─");
                            Check.True(startA > 0 && startA == startB, "both plot regions start in the same column");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("DistributionCharts", "HistogramBucketSum", "Bucket counts sum to the sample count",
                        _ =>
                        {
                            Histogram histogram = new Histogram();
                            histogram.BucketCount = 5;
                            histogram.SetValues(new double[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });

                            IReadOnlyList<int> counts = histogram.ComputeBucketCounts();
                            Check.Equal(5, counts.Count, "one entry per bucket");
                            int sum = 0;
                            for (int i = 0; i < counts.Count; i++)
                                sum += counts[i];
                            Check.Equal(10, sum, "every sample lands in exactly one bucket");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("DistributionCharts", "HistogramTallest", "The tallest bucket reaches full height",
                        _ =>
                        {
                            Histogram histogram = new Histogram();
                            histogram.SetValues(new double[] { 7, 7, 7, 7 });

                            CellBuffer buffer = new CellBuffer(20, 4);
                            histogram.Render(new BufferSurface(buffer));
                            Check.Equal("█", buffer.Get(0, 0).Grapheme, "the full bucket fills to the top row");

                            Histogram empty = new Histogram();
                            Check.Equal(0, empty.Measure(new Size(20, 4)).Height, "empty measures to zero");
                            CellBuffer blank = new CellBuffer(20, 4);
                            empty.Render(new BufferSurface(blank));
                            Check.False(HasInk(blank), "empty draws nothing");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("DistributionCharts", "HistogramGuards", "Bucket count clamps and inputs are guarded",
                        _ =>
                        {
                            Histogram histogram = new Histogram();
                            histogram.BucketCount = 0;
                            Check.Equal(1, histogram.BucketCount, "bucket count clamps at 1");
                            histogram.BucketCount = -4;
                            Check.Equal(1, histogram.BucketCount, "negative bucket count clamps at 1");

                            Check.Throws<ArgumentNullException>(() => histogram.SetValues(null!), "null values rejected");
                            Check.Throws<ArgumentOutOfRangeException>(() => histogram.Push(1, 0), "non-positive capacity rejected");
                            Check.Throws<ArgumentException>(() => histogram.SetRange(5, 5), "empty range rejected");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("DistributionCharts", "HistogramFixedRange", "A fixed range buckets identically across series",
                        _ =>
                        {
                            Histogram first = new Histogram();
                            first.BucketCount = 10;
                            first.SetRange(0, 100);
                            first.SetValues(new double[] { 50 });

                            Histogram second = new Histogram();
                            second.BucketCount = 10;
                            second.SetRange(0, 100);
                            second.SetValues(new double[] { 50, 90 });

                            IReadOnlyList<int> a = first.ComputeBucketCounts();
                            IReadOnlyList<int> b = second.ComputeBucketCounts();
                            Check.Equal(1, a[5], "50 falls in bucket 5 under the fixed range");
                            Check.Equal(1, b[5], "50 falls in the same bucket regardless of the other samples");
                            Check.Equal(1, b[9], "90 falls in the top bucket");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("DistributionCharts", "HeatMapNormalize", "Max cell is densest, min cell is lightest",
                        _ =>
                        {
                            HeatMap map = new HeatMap();
                            map.SetCells(new double[,] { { 0, 1 }, { 2, 3 } });

                            CellBuffer buffer = new CellBuffer(2, 2);
                            map.Render(new BufferSurface(buffer));
                            Check.Equal("░", buffer.Get(0, 0).Grapheme, "the minimum cell is the lightest shade");
                            Check.Equal("█", buffer.Get(1, 1).Grapheme, "the maximum cell is the densest shade");

                            Check.Throws<ArgumentNullException>(() => map.SetCells(null!), "null cells rejected");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("DistributionCharts", "HeatMapLabels", "Row and column labels offset the grid",
                        _ =>
                        {
                            HeatMap map = new HeatMap();
                            map.SetCells(new double[,] { { 1, 2 }, { 3, 4 } });
                            map.SetRowLabels(new[] { "a", "b" });
                            map.SetColumnLabels(new[] { "X", "Y" });

                            CellBuffer buffer = new CellBuffer(8, 4);
                            map.Render(new BufferSurface(buffer));

                            // Row-label gutter is 1 + 1 = 2; a column-label header consumes row 0.
                            Check.Equal("a", buffer.Get(0, 1).Grapheme, "row label sits in the gutter");
                            Check.Equal("X", buffer.Get(2, 0).Grapheme, "column label sits in the header");
                            Check.True(IsShade(buffer.Get(2, 1).Grapheme), "the grid starts after the gutter and header");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("DistributionCharts", "HeatMapFixedRange", "A fixed range changes the shading",
                        _ =>
                        {
                            HeatMap derived = new HeatMap();
                            derived.SetCells(new double[,] { { 0, 10 } });

                            HeatMap fixedRange = new HeatMap();
                            fixedRange.SetCells(new double[,] { { 0, 10 } });
                            fixedRange.SetRange(0, 100);

                            CellBuffer a = new CellBuffer(2, 1);
                            CellBuffer b = new CellBuffer(2, 1);
                            derived.Render(new BufferSurface(a));
                            fixedRange.Render(new BufferSurface(b));

                            Check.Equal("█", a.Get(1, 0).Grapheme, "under the derived range the high cell is densest");
                            Check.True(a.Get(1, 0).Grapheme != b.Get(1, 0).Grapheme, "widening the range lightens the same cell");

                            HeatMap empty = new HeatMap();
                            Check.Equal(0, empty.Measure(new Size(4, 4)).Height, "empty measures to zero");
                            CellBuffer blank = new CellBuffer(4, 4);
                            empty.Render(new BufferSurface(blank));
                            Check.False(HasInk(blank), "empty draws nothing");
                            return Task.CompletedTask;
                        })
                });
        }

        private static bool HasInk(CellBuffer buffer)
        {
            for (int y = 0; y < buffer.Height; y++)
            {
                for (int x = 0; x < buffer.Width; x++)
                {
                    string g = buffer.Get(x, y).Grapheme;
                    if (!string.IsNullOrEmpty(g) && g != " ")
                        return true;
                }
            }

            return false;
        }

        private static bool IsShade(string grapheme)
        {
            return grapheme == "░" || grapheme == "▒" || grapheme == "▓" || grapheme == "█";
        }

        private static int FirstGlyphCol(CellBuffer buffer, int row, string glyph)
        {
            for (int x = 0; x < buffer.Width; x++)
            {
                if (buffer.Get(x, row).Grapheme == glyph)
                    return x;
            }

            return -1;
        }

        private static int LastGlyphCol(CellBuffer buffer, int row, string glyph)
        {
            for (int x = buffer.Width - 1; x >= 0; x--)
            {
                if (buffer.Get(x, row).Grapheme == glyph)
                    return x;
            }

            return -1;
        }

        private static int CountGlyph(CellBuffer buffer, int row, string glyph)
        {
            int count = 0;
            for (int x = 0; x < buffer.Width; x++)
            {
                if (buffer.Get(x, row).Grapheme == glyph)
                    count++;
            }

            return count;
        }
    }
}

namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Testing;
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for frame geometry and title placement (1.5.0, B7): <see cref="FocusFrame.ContentRect"/>,
    /// <see cref="FocusFrame.OuterRect"/>, <see cref="FocusFrame.UsesGutter"/>, <see cref="TitleAlignment"/>,
    /// <see cref="FocusFrameOptions.TitleAlignment"/>, and <see cref="FocusFrameOptions.TitleInset"/>.
    /// </summary>
    public static class FrameGeometrySuite
    {
        /// <summary>
        /// Builds the frame geometry suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "FrameGeometry",
                displayName: "Frame Geometry and Title Alignment",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("FrameGeometry", "ContentRectMatchesDraw", "From 1x1 to 40x20, ContentRect is exactly the area Draw leaves untouched",
                        _ =>
                        {
                            FocusFrameOptions options = new FocusFrameOptions();
                            for (int width = 1; width <= 40; width++)
                            {
                                for (int height = 1; height <= 20; height++)
                                {
                                    Rect outer = new Rect(1, 1, width, height);
                                    Rect content = FocusFrame.ContentRect(outer, options);
                                    foreach (bool focused in new[] { true, false })
                                    {
                                        CellBuffer buffer = Filled(width + 2, height + 2);
                                        FocusFrame.Draw(new BufferSurface(buffer), outer, focused, CellStyle.Default, CellStyle.Default, false, options, "T");
                                        for (int y = outer.Top; y < outer.Bottom; y++)
                                        {
                                            for (int x = outer.Left; x < outer.Right; x++)
                                            {
                                                bool inside = content.Contains(new Point(x, y));
                                                bool untouched = buffer.Get(x, y).Grapheme == "x";
                                                if (inside != untouched)
                                                    throw new InvalidOperationException("Size " + width + "x" + height + (focused ? " focused" : " unfocused") + ": cell " + x + "," + y + (inside ? " is content but was drawn" : " is frame but was not drawn") + ".");
                                            }
                                        }
                                    }

                                    if (!FocusFrame.UsesGutter(outer, options) && width >= 3 && height >= 3)
                                        Check.Equal(outer, FocusFrame.OuterRect(content, options), "OuterRect inverts ContentRect at " + width + "x" + height);
                                }
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FrameGeometry", "GutterAndBoxBoundary", "UsesGutter and ContentRect switch exactly at MinimumBoxSize and MinimumGutterWidth",
                        _ =>
                        {
                            FocusFrameOptions options = new FocusFrameOptions();
                            Check.False(FocusFrame.UsesGutter(new Rect(0, 0, 3, 3), options), "3x3 is a box");
                            Check.True(FocusFrame.UsesGutter(new Rect(0, 0, 2, 9), options), "2 wide is a gutter");
                            Check.True(FocusFrame.UsesGutter(new Rect(0, 0, 9, 2), options), "2 high is a gutter");
                            Check.Equal(new Rect(1, 1, 1, 1), FocusFrame.ContentRect(new Rect(0, 0, 3, 3), options), "3x3 leaves one content cell");
                            Check.Equal(new Rect(5, 2, 8, 2), FocusFrame.ContentRect(new Rect(4, 2, 9, 2), options), "gutter content skips the first column");
                            Check.True(FocusFrame.ContentRect(new Rect(0, 0, 0, 5), options).IsEmpty, "empty in, empty out");
                            Check.Equal(new Rect(3, 4, 2, 1), FocusFrame.OuterRect(new Rect(4, 4, 1, 1), new FocusFrameOptions { MinimumBoxSize = 4 }), "too small for a box: OuterRect uses the gutter form");
                            Check.True(FocusFrame.OuterRect(new Rect(2, 2, 0, 0), options).IsEmpty, "empty content stays empty");

                            options.MinimumGutterWidth = 2;
                            Check.False(FocusFrame.UsesGutter(new Rect(0, 0, 1, 5), options), "1 wide is below MinimumGutterWidth");
                            Check.Equal(new Rect(0, 0, 1, 5), FocusFrame.ContentRect(new Rect(0, 0, 1, 5), options), "no gutter: all of it is content");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FrameGeometry", "TitleAlignment", "Left, center, and right titles land in the expected columns, with the focus marker",
                        _ =>
                        {
                            Check.Equal("┌─ Log ────────────┐", TopRow(TitleAlignment.Left, 1, "Log", 20), "left, inset 1");
                            Check.Equal("┌ Log ─────────────┐", TopRow(TitleAlignment.Left, 0, "Log", 20), "left, inset 0");
                            Check.Equal("┌──────────── Log ─┐", TopRow(TitleAlignment.Right, 1, "Log", 20), "right, inset 1");
                            Check.Equal("┌────────── Log ───┐", TopRow(TitleAlignment.Right, 3, "Log", 20), "right, inset 3");
                            Check.Equal("┌────── Log ───────┐", TopRow(TitleAlignment.Center, 4, "Log", 20), "center ignores the inset");

                            CellBuffer buffer = new CellBuffer(20, 4);
                            FocusFrameOptions options = new FocusFrameOptions { TitleAlignment = TitleAlignment.Left };
                            FocusFrame.Draw(new BufferSurface(buffer), new Rect(0, 0, 20, 4), true, CellStyle.Default, CellStyle.Default, false, options, "Log");
                            Check.Equal("┏━ > Log ━━━━━━━━━━┓", Snapshot.ToText(buffer).Split('\n')[0], "focused left title carries the marker");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FrameGeometry", "LongTitleTruncates", "A long title is truncated and never overwrites a corner, in every alignment",
                        _ =>
                        {
                            foreach (TitleAlignment alignment in new[] { TitleAlignment.Left, TitleAlignment.Center, TitleAlignment.Right })
                            {
                                foreach (int inset in new[] { 0, 1, 4 })
                                {
                                    string row = TopRow(alignment, inset, "A much longer title than fits", 12);
                                    Check.Equal(12, row.Length, alignment + " inset " + inset + " keeps the row width");
                                    Check.Equal('┌', row[0], alignment + " inset " + inset + " keeps the left corner");
                                    Check.Equal('┐', row[11], alignment + " inset " + inset + " keeps the right corner");
                                }
                            }

                            Check.Equal("┌┐", TopRow(TitleAlignment.Left, 1, "Log", 2), "no room: no title");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("FrameGeometry", "InsetAndNullGuards", "TitleInset outside 0 to 4 and null options are rejected",
                        _ =>
                        {
                            FocusFrameOptions options = new FocusFrameOptions();
                            Check.Throws<ArgumentOutOfRangeException>(() => options.TitleInset = -1, "inset -1");
                            Check.Throws<ArgumentOutOfRangeException>(() => options.TitleInset = 5, "inset 5");
                            options.TitleInset = 0;
                            options.TitleInset = 4;
                            Check.Equal(4, options.TitleInset, "inset 4 accepted");
                            Check.Equal(TitleAlignment.Center, new FocusFrameOptions().TitleAlignment, "center by default");

                            BufferSurface surface = new BufferSurface(new CellBuffer(10, 3));
                            Check.Throws<ArgumentOutOfRangeException>(() => surface.DrawBox(new Rect(0, 0, 10, 3), CellStyle.Default, BorderStyle.Line, "T", CellStyle.Default, TitleAlignment.Left, 5), "DrawBox inset 5");
                            Check.Throws<ArgumentOutOfRangeException>(() => surface.DrawJoinedBox(new Rect(0, 0, 10, 3), CellStyle.Default, BorderStyle.Line, "T", CellStyle.Default, JoinMode.Merge, TitleAlignment.Right, -1), "DrawJoinedBox inset -1");
                            Check.Throws<ArgumentNullException>(() => SurfaceExtensions.DrawBox(null!, new Rect(0, 0, 4, 4), CellStyle.Default, BorderStyle.Line, "T", CellStyle.Default, TitleAlignment.Left, 1), "null surface");
                            Check.Throws<ArgumentNullException>(() => FocusFrame.ContentRect(new Rect(0, 0, 4, 4), null!), "ContentRect null options");
                            Check.Throws<ArgumentNullException>(() => FocusFrame.OuterRect(new Rect(0, 0, 4, 4), null!), "OuterRect null options");
                            Check.Throws<ArgumentNullException>(() => FocusFrame.UsesGutter(new Rect(0, 0, 4, 4), null!), "UsesGutter null options");
                            return Task.CompletedTask;
                        })
                });
        }

        private static CellBuffer Filled(int width, int height)
        {
            CellBuffer buffer = new CellBuffer(width, height);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                    buffer.Set(x, y, Cell.Glyph("x", CellStyle.Default, 1));
            }

            return buffer;
        }

        private static string TopRow(TitleAlignment alignment, int inset, string title, int width)
        {
            CellBuffer buffer = new CellBuffer(width, 3);
            new BufferSurface(buffer).DrawBox(new Rect(0, 0, width, 3), CellStyle.Default, BorderStyle.Line, title, CellStyle.Default, alignment, inset);
            return Snapshot.ToText(buffer).Split('\n')[0];
        }
    }
}

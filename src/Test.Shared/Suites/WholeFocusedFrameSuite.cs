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
    /// Coverage for focused frames drawn whole where they join neighbours (1.5.0, B2): <see cref="JoinMode"/>,
    /// the <see cref="SurfaceExtensions.DrawJoinedBox(ISurface, Rect, CellStyle, BorderStyle, string?, CellStyle, JoinMode)"/>
    /// overload, <see cref="FocusFrameOptions.FocusedJoinMode"/>, and <see cref="FocusFrameOptions.UnfocusedJoinMode"/>.
    /// </summary>
    public static class WholeFocusedFrameSuite
    {
        private const string HeavyOnly = "━┃┏┓┗┛┣┫┳┻╋";

        /// <summary>
        /// Builds the whole focused frame suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "WholeFocusedFrame",
                displayName: "Focused Frames Drawn Whole",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("WholeFocusedFrame", "AdjacentFocusedIsHeavyOnly", "Of two adjacent frames, the focused outline is heavy only and its neighbour stays light; swapping focus swaps them",
                        _ =>
                        {
                            CellBuffer buffer = TwoFrames(true);
                            AssertHeavyOutline(buffer, new Rect(0, 0, 11, 7), "left focused");
                            Check.Equal("┳", buffer.Get(10, 0).Grapheme, "the right frame's edge joins the focused outline at a heavy tee");
                            Check.Equal("─", buffer.Get(11, 0).Grapheme, "right frame's line is light next to it");
                            Check.Equal("┐", buffer.Get(20, 0).Grapheme, "right frame's far corner is light");

                            buffer = TwoFrames(false);
                            AssertHeavyOutline(buffer, new Rect(10, 0, 11, 7), "right focused");
                            Check.Equal("─", buffer.Get(9, 0).Grapheme, "left frame's line is light now");
                            Check.Equal("┌", buffer.Get(0, 0).Grapheme, "left frame's corner is light");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("WholeFocusedFrame", "NestedFocusedKeepsOuterUnbroken", "A focused frame inside a light frame is whole, and the outer line runs through it unbroken",
                        _ =>
                        {
                            CellBuffer buffer = new CellBuffer(20, 8);
                            BufferSurface surface = new BufferSurface(buffer);
                            FocusFrameOptions options = new FocusFrameOptions { JoinBorders = true };
                            FocusFrame.Draw(surface, new Rect(0, 0, 20, 8), false, CellStyle.Default, CellStyle.Default, false, options);
                            FocusFrame.Draw(surface, new Rect(5, 0, 8, 4), true, CellStyle.Default, CellStyle.Default, false, options);
                            string[] rows = Snapshot.ToText(buffer).Split('\n');
                            Check.Equal("┌────┳━━━━━━┳──────┐", rows[0], "outer top line runs through the focused frame");
                            Check.Equal("│    ┗━━━━━━┛      │", rows[3], "focused frame bottom is whole");
                            AssertHeavyOutline(buffer, new Rect(5, 0, 8, 4), "nested");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("WholeFocusedFrame", "AsciiFocusedOutline", "With ASCII borders the focused outline is all # and =, with no +",
                        _ =>
                        {
                            CellBuffer buffer = new CellBuffer(21, 5);
                            BufferSurface surface = new BufferSurface(buffer);
                            FocusFrameOptions options = new FocusFrameOptions { JoinBorders = true };
                            FocusFrame.Draw(surface, new Rect(10, 0, 11, 5), false, CellStyle.Default, CellStyle.Default, true, options);
                            FocusFrame.Draw(surface, new Rect(0, 0, 11, 5), true, CellStyle.Default, CellStyle.Default, true, options);
                            foreach (Point cell in Outline(new Rect(0, 0, 11, 5)))
                            {
                                string glyph = buffer.Get(cell.X, cell.Y).Grapheme;
                                Check.True(glyph == "#" || glyph == "=", "focused ASCII cell " + cell.X + "," + cell.Y + " is " + glyph);
                            }

                            Check.Equal("-", buffer.Get(15, 0).Grapheme, "neighbour keeps its light ASCII line");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("WholeFocusedFrame", "MergeRestores14", "FocusedJoinMode = Merge reproduces the 1.4.0 output cell for cell",
                        _ =>
                        {
                            CellBuffer expected = new CellBuffer(21, 7);
                            BufferSurface plain = new BufferSurface(expected);
                            plain.DrawJoinedBox(new Rect(10, 0, 11, 7), CellStyle.Default, BorderStyle.Line);
                            plain.DrawJoinedBox(new Rect(0, 0, 11, 7), CellStyle.Default, BorderStyle.Thick, null, CellStyle.Default);

                            CellBuffer actual = new CellBuffer(21, 7);
                            BufferSurface surface = new BufferSurface(actual);
                            FocusFrameOptions options = new FocusFrameOptions { JoinBorders = true, FocusedJoinMode = JoinMode.Merge };
                            FocusFrame.Draw(surface, new Rect(10, 0, 11, 7), false, CellStyle.Default, CellStyle.Default, false, options);
                            FocusFrame.Draw(surface, new Rect(0, 0, 11, 7), true, CellStyle.Default, CellStyle.Default, false, options);
                            for (int y = 0; y < 7; y++)
                            {
                                for (int x = 0; x < 21; x++)
                                    Check.Equal(expected.Get(x, y), actual.Get(x, y), "cell " + x + "," + y);
                            }

                            Check.Equal("┱", actual.Get(10, 0).Grapheme, "the 1.4.0 mixed junction");
                            Check.Equal(JoinMode.OverlayWhole, new FocusFrameOptions().FocusedJoinMode, "focused default is OverlayWhole");
                            Check.Equal(JoinMode.Merge, new FocusFrameOptions().UnfocusedJoinMode, "unfocused default is Merge");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("WholeFocusedFrame", "NoneDrawsPlain", "JoinMode.None draws a plain box over existing lines",
                        _ =>
                        {
                            CellBuffer buffer = new CellBuffer(21, 5);
                            BufferSurface surface = new BufferSurface(buffer);
                            surface.DrawJoinedBox(new Rect(0, 0, 21, 5), CellStyle.Default, BorderStyle.Line);
                            surface.DrawJoinedBox(new Rect(10, 0, 11, 5), CellStyle.Default, BorderStyle.Line, null, CellStyle.Default, JoinMode.None);
                            Check.Equal("┌", buffer.Get(10, 0).Grapheme, "plain corner over the outer line");
                            surface.DrawJoinedBox(new Rect(0, 0, 11, 5), CellStyle.Default, BorderStyle.Double, null, CellStyle.Default, JoinMode.OverlayWhole);
                            Check.Equal("╦", buffer.Get(10, 0).Grapheme, "double whole: the light line under it takes the double weight");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("WholeFocusedFrame", "Guards", "The join-mode overload rejects a null surface and draws nothing for an empty or tiny rect",
                        _ =>
                        {
                            Check.Throws<ArgumentNullException>(() => SurfaceExtensions.DrawJoinedBox(null!, new Rect(0, 0, 4, 4), CellStyle.Default, BorderStyle.Line, null, CellStyle.Default, JoinMode.OverlayWhole), "null surface");
                            CellBuffer buffer = new CellBuffer(6, 4);
                            BufferSurface surface = new BufferSurface(buffer);
                            surface.DrawJoinedBox(new Rect(0, 0, 0, 0), CellStyle.Default, BorderStyle.Thick, null, CellStyle.Default, JoinMode.OverlayWhole);
                            surface.DrawJoinedBox(new Rect(0, 0, 1, 4), CellStyle.Default, BorderStyle.Thick, null, CellStyle.Default, JoinMode.OverlayWhole);
                            Check.Equal(string.Empty, Snapshot.ToText(buffer).Trim(), "nothing drawn");
                            return Task.CompletedTask;
                        })
                });
        }

        private static CellBuffer TwoFrames(bool leftFocused)
        {
            CellBuffer buffer = new CellBuffer(21, 7);
            BufferSurface surface = new BufferSurface(buffer);
            FocusFrameOptions options = new FocusFrameOptions { JoinBorders = true };
            Rect left = new Rect(0, 0, 11, 7);
            Rect right = new Rect(10, 0, 11, 7);
            FocusFrame.Draw(surface, leftFocused ? right : left, false, CellStyle.Default, CellStyle.Default, false, options);
            FocusFrame.Draw(surface, leftFocused ? left : right, true, CellStyle.Default, CellStyle.Default, false, options);
            return buffer;
        }

        private static void AssertHeavyOutline(CellBuffer buffer, Rect rect, string context)
        {
            foreach (Point cell in Outline(rect))
            {
                string glyph = buffer.Get(cell.X, cell.Y).Grapheme;
                Check.True(glyph.Length == 1 && HeavyOnly.IndexOf(glyph, StringComparison.Ordinal) >= 0, context + ": cell " + cell.X + "," + cell.Y + " is '" + glyph + "', not heavy only");
            }
        }

        private static List<Point> Outline(Rect rect)
        {
            List<Point> cells = new List<Point>();
            for (int x = rect.Left; x < rect.Right; x++)
            {
                cells.Add(new Point(x, rect.Top));
                cells.Add(new Point(x, rect.Bottom - 1));
            }

            for (int y = rect.Top + 1; y < rect.Bottom - 1; y++)
            {
                cells.Add(new Point(rect.Left, y));
                cells.Add(new Point(rect.Right - 1, y));
            }

            return cells;
        }
    }
}

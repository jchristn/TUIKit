namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;

    /// <summary>
    /// Touchstone suite covering <see cref="CellBuffer"/> storage and dirty tracking, and drawing
    /// through <see cref="BufferSurface"/> and <see cref="SurfaceExtensions"/>.
    /// </summary>
    public static class BufferSurfaceSuite
    {
        /// <summary>
        /// Builds the buffer and surface suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "BufferSurface",
                displayName: "Cell Buffer and Surface",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("BufferSurface", "SetAndDirty", "Set marks only changed rows dirty",
                        _ =>
                        {
                            CellBuffer buffer = new CellBuffer(10, 4);
                            buffer.ClearDirty();
                            Check.False(buffer.IsRowDirty(1), "Row 1 clean after ClearDirty.");

                            buffer.Set(3, 1, Cell.Glyph("X", CellStyle.Default, 1));
                            Check.True(buffer.IsRowDirty(1), "Row 1 dirty after Set.");
                            Check.False(buffer.IsRowDirty(2), "Row 2 still clean.");
                            Check.Equal("X", buffer.Get(3, 1).Grapheme, "Written glyph");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("BufferSurface", "SetNoChangeNoDirty", "Writing an identical cell is not dirty",
                        _ =>
                        {
                            CellBuffer buffer = new CellBuffer(4, 4);
                            buffer.ClearDirty();
                            buffer.Set(0, 0, Cell.Empty);
                            Check.False(buffer.IsRowDirty(0), "Identical write must not dirty the row.");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("BufferSurface", "OutOfBoundsGetThrows", "Get out of bounds throws",
                        _ =>
                        {
                            CellBuffer buffer = new CellBuffer(4, 4);
                            Check.Throws<ArgumentOutOfRangeException>(() => buffer.Get(4, 0), "Column out of range");
                            Check.Throws<ArgumentOutOfRangeException>(() => buffer.Get(0, 4), "Row out of range");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("BufferSurface", "Resize", "Resize preserves overlapping content",
                        _ =>
                        {
                            CellBuffer buffer = new CellBuffer(4, 2);
                            buffer.Set(1, 1, Cell.Glyph("Q", CellStyle.Default, 1));
                            buffer.Resize(6, 3);
                            Check.Equal(6, buffer.Width, "New width");
                            Check.Equal(3, buffer.Height, "New height");
                            Check.Equal("Q", buffer.Get(1, 1).Grapheme, "Preserved content");
                            Check.Equal(" ", buffer.Get(5, 2).Grapheme, "New area is blank");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("BufferSurface", "DrawTextWide", "DrawText lays out a wide glyph with continuation",
                        _ =>
                        {
                            CellBuffer buffer = new CellBuffer(6, 1);
                            BufferSurface surface = new BufferSurface(buffer);
                            int advanced = surface.DrawText(0, 0, "A世B", CellStyle.Default);
                            Check.Equal(4, advanced, "Columns advanced (1 + 2 + 1)");
                            Check.Equal("A", buffer.Get(0, 0).Grapheme, "Cell 0 narrow");
                            Check.Equal(2, buffer.Get(1, 0).Width, "Cell 1 is the wide leading half");
                            Check.True(buffer.Get(2, 0).IsContinuation, "Cell 2 is a continuation");
                            Check.Equal("B", buffer.Get(3, 0).Grapheme, "Cell 3 narrow after wide glyph");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("BufferSurface", "ViewTranslation", "A view translates and clips writes",
                        _ =>
                        {
                            CellBuffer buffer = new CellBuffer(10, 5);
                            BufferSurface surface = new BufferSurface(buffer);
                            BufferSurface view = surface.CreateView(new Rect(2, 1, 3, 2));
                            Check.Equal(new Size(3, 2), view.Size, "View size");

                            view.Set(0, 0, Cell.Glyph("V", CellStyle.Default, 1));
                            Check.Equal("V", buffer.Get(2, 1).Grapheme, "View origin maps to (2,1)");

                            view.Set(2, 1, Cell.Glyph("W", CellStyle.Default, 1)); // last valid view cell
                            Check.Equal("W", buffer.Get(4, 2).Grapheme, "View (2,1) maps to buffer (4,2)");

                            view.Set(3, 0, Cell.Glyph("Z", CellStyle.Default, 1)); // column 3 is outside the 3-wide view
                            Check.Equal(" ", buffer.Get(5, 1).Grapheme, "Out-of-view write is clipped");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}

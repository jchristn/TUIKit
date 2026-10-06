namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for the narrow-space gutter (1.5.0, B9): <see cref="FocusFrameOptions.FocusedGutterGlyph"/>
    /// and its <see cref="FocusFrameOptions.GutterGlyph"/> alias, the ASCII and unfocused gutter glyphs,
    /// <see cref="FocusFrameOptions.MinimumGutterWidth"/>, and <see cref="FocusFrame.ApplyNarrowFocus"/>.
    /// </summary>
    public static class GutterFallbackSuite
    {
        /// <summary>
        /// Builds the gutter fallback suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "GutterFallback",
                displayName: "Gutter Fallback in Every Mode",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("GutterFallback", "AsciiGutterDiffersByGlyph", "With ASCII borders the focused gutter is # and the unfocused gutter is blank",
                        _ =>
                        {
                            FocusFrameOptions options = new FocusFrameOptions();
                            Check.Equal("#", Gutter(options, true, true).Grapheme, "focused ASCII gutter");
                            Check.Equal(" ", Gutter(options, false, true).Grapheme, "unfocused ASCII gutter is written blank");
                            Check.Equal("▌", Gutter(options, true, false).Grapheme, "focused Unicode gutter keeps the 1.4.0 half block");
                            Check.Equal(" ", Gutter(options, false, false).Grapheme, "unfocused Unicode gutter is written blank");

                            options.AsciiFocusedGutterGlyph = ">";
                            options.UnfocusedGutterGlyph = "│";
                            Check.Equal(">", Gutter(options, true, true).Grapheme, "custom ASCII focused glyph");
                            Check.Equal("│", Gutter(options, false, false).Grapheme, "custom unfocused glyph");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("GutterFallback", "GutterGlyphAlias", "Setting GutterGlyph sets FocusedGutterGlyph and the rendered focused gutter, as in 1.4.0",
                        _ =>
                        {
                            FocusFrameOptions options = new FocusFrameOptions { GutterGlyph = "┃" };
                            Check.Equal("┃", options.FocusedGutterGlyph, "alias writes the new property");
                            options.FocusedGutterGlyph = "█";
                            Check.Equal("█", options.GutterGlyph, "alias reads the new property");
                            Check.Equal("█", Gutter(options, true, false).Grapheme, "rendered focused gutter");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("GutterFallback", "UnfocusedClearsStaleColumn", "An unfocused gutter overwrites whatever an earlier frame left in its column",
                        _ =>
                        {
                            CellBuffer buffer = new CellBuffer(2, 4);
                            BufferSurface surface = new BufferSurface(buffer);
                            FocusFrameOptions options = new FocusFrameOptions();
                            FocusFrame.Draw(surface, new Rect(0, 0, 2, 4), true, CellStyle.Default, CellStyle.Default, false, options);
                            Check.Equal("▌", buffer.Get(0, 2).Grapheme, "focused gutter drawn");
                            FocusFrame.Draw(surface, new Rect(0, 0, 2, 4), false, CellStyle.Default, CellStyle.Default, false, options);
                            for (int y = 0; y < 4; y++)
                                Check.Equal(" ", buffer.Get(0, y).Grapheme, "row " + y + " cleared");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("GutterFallback", "MinimumGutterWidth", "Below MinimumGutterWidth there is no gutter, and focus is a reverse first column that survives content",
                        _ =>
                        {
                            CellBuffer legacy = new CellBuffer(1, 3);
                            FocusFrame.Draw(new BufferSurface(legacy), new Rect(0, 0, 1, 3), true, CellStyle.Default, CellStyle.Default, false, new FocusFrameOptions());
                            Check.Equal("▌", legacy.Get(0, 1).Grapheme, "default 1: a 1-wide rect is all gutter, as in 1.4.0");

                            FocusFrameOptions options = new FocusFrameOptions { MinimumGutterWidth = 2 };
                            CellBuffer buffer = new CellBuffer(1, 3);
                            BufferSurface surface = new BufferSurface(buffer);
                            Rect rect = new Rect(0, 0, 1, 3);
                            FocusFrame.Draw(surface, rect, true, CellStyle.Default, CellStyle.Default, false, options);
                            Check.True(buffer.Get(0, 0).Style.Attributes.HasFlag(CellAttributes.Reverse), "focused bare column is reverse");

                            buffer.Set(0, 0, Cell.Glyph("a", CellStyle.Default, 1));
                            FocusFrame.ApplyNarrowFocus(surface, rect, true, options);
                            Check.Equal("a", buffer.Get(0, 0).Grapheme, "content glyph kept");
                            Check.True(buffer.Get(0, 0).Style.Attributes.HasFlag(CellAttributes.Reverse), "reverse applied after content");

                            CellBuffer unfocused = new CellBuffer(1, 3);
                            unfocused.Set(0, 0, Cell.Glyph("a", CellStyle.Default, 1));
                            FocusFrame.Draw(new BufferSurface(unfocused), rect, false, CellStyle.Default, CellStyle.Default, false, options);
                            FocusFrame.ApplyNarrowFocus(new BufferSurface(unfocused), rect, false, options);
                            Check.Equal("a", unfocused.Get(0, 0).Grapheme, "unfocused bare column untouched");
                            Check.False(unfocused.Get(0, 0).Style.Attributes.HasFlag(CellAttributes.Reverse), "no reverse when unfocused");

                            CellBuffer box = new CellBuffer(5, 5);
                            box.Set(0, 0, Cell.Glyph("a", CellStyle.Default, 1));
                            FocusFrame.ApplyNarrowFocus(new BufferSurface(box), new Rect(0, 0, 5, 5), true, options);
                            Check.False(box.Get(0, 0).Style.Attributes.HasFlag(CellAttributes.Reverse), "a full box is never reversed");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("GutterFallback", "SplitViewNarrowPane", "A SplitView pane too narrow for a gutter keeps its content column and shows focus in reverse",
                        _ =>
                        {
                            TextField left = new TextField { Value = "L" };
                            TextField right = new TextField { Value = "R" };
                            SplitView split = new SplitView(SplitOrientation.Horizontal, left, right, 0.05) { ForwardKeys = true, ShowPaneFrames = true };
                            split.FrameOptions.MinimumGutterWidth = 2;
                            split.OnFocusChanged(true);
                            TUIKit.Testing.WidgetTester tester = TUIKit.Testing.WidgetTester.For(split, 12, 3).Render();
                            Cell first = tester.CellAt(0, 0);
                            Check.Equal("L", first.Grapheme, "the 1-wide pane shows its content");
                            Check.True(first.Style.Attributes.HasFlag(CellAttributes.Reverse), "and its focus in reverse");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("GutterFallback", "Guards", "Gutter options reject out-of-range widths, null glyphs, and glyphs that are not one cell",
                        _ =>
                        {
                            FocusFrameOptions options = new FocusFrameOptions();
                            Check.Equal(1, options.MinimumGutterWidth, "default 1");
                            Check.Throws<ArgumentOutOfRangeException>(() => options.MinimumGutterWidth = 0, "width 0");
                            Check.Throws<ArgumentOutOfRangeException>(() => options.MinimumGutterWidth = 9, "width 9");
                            options.MinimumGutterWidth = 8;
                            Check.Throws<ArgumentNullException>(() => options.FocusedGutterGlyph = null!, "null focused glyph");
                            Check.Throws<ArgumentNullException>(() => options.AsciiUnfocusedGutterGlyph = null!, "null ASCII unfocused glyph");
                            Check.Throws<ArgumentNullException>(() => options.GutterGlyph = null!, "null alias");
                            Check.Throws<ArgumentException>(() => options.UnfocusedGutterGlyph = string.Empty, "empty glyph");
                            Check.Throws<ArgumentException>(() => options.AsciiFocusedGutterGlyph = "##", "two cells");
                            Check.Throws<ArgumentException>(() => options.FocusedGutterGlyph = "界", "wide glyph");
                            Check.Throws<ArgumentNullException>(() => FocusFrame.ApplyNarrowFocus(null!, new Rect(0, 0, 1, 1), true, options), "null surface");
                            Check.Throws<ArgumentNullException>(() => FocusFrame.ApplyNarrowFocus(new BufferSurface(new CellBuffer(1, 1)), new Rect(0, 0, 1, 1), true, null!), "null options");
                            return Task.CompletedTask;
                        })
                });
        }

        private static Cell Gutter(FocusFrameOptions options, bool focused, bool ascii)
        {
            CellBuffer buffer = new CellBuffer(2, 3);
            buffer.Set(0, 1, Cell.Glyph("x", CellStyle.Default, 1));
            FocusFrame.Draw(new BufferSurface(buffer), new Rect(0, 0, 2, 3), focused, CellStyle.Default, CellStyle.Default, ascii, options);
            return buffer.Get(0, 1);
        }
    }
}

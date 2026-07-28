namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;

    /// <summary>
    /// Touchstone suite covering the <see cref="Color"/>, <see cref="CellStyle"/>, and
    /// <see cref="Cell"/> types.
    /// </summary>
    public static class ColorStyleSuite
    {
        /// <summary>
        /// Builds the color and style suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "ColorStyle",
                displayName: "Color, Style, and Cell",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("ColorStyle", "RgbPacking", "Packed RGB decodes to channels",
                        _ =>
                        {
                            Color c = Color.FromRgb(0xFF8800);
                            Check.Equal(ColorKind.Rgb, c.Kind, "Kind");
                            Check.Equal((byte)0xFF, c.R, "R");
                            Check.Equal((byte)0x88, c.G, "G");
                            Check.Equal((byte)0x00, c.B, "B");
                            Check.True(c == Color.FromRgb(255, 136, 0), "Channel and packed forms are equal.");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ColorStyle", "ColorKindEquality", "Colors of different kinds differ",
                        _ =>
                        {
                            Check.False(Color.Default == Color.FromPalette(0), "Default != palette 0.");
                            Check.False(Color.FromPalette(1) == Color.FromRgb(0, 0, 0), "Palette != rgb.");
                            Check.True(Color.FromPalette(5) == Color.FromPalette(5), "Same palette index is equal.");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ColorStyle", "StyleWith", "Style derivation and attribute toggling",
                        _ =>
                        {
                            CellStyle style = CellStyle.Default
                                .WithForeground(Color.FromPalette(1))
                                .WithAttribute(CellAttributes.Bold, true)
                                .WithAttribute(CellAttributes.Underline, true);
                            Check.True(style.HasAttribute(CellAttributes.Bold), "Bold present.");
                            Check.True(style.HasAttribute(CellAttributes.Underline), "Underline present.");

                            CellStyle without = style.WithAttribute(CellAttributes.Bold, false);
                            Check.False(without.HasAttribute(CellAttributes.Bold), "Bold removed.");
                            Check.True(without.HasAttribute(CellAttributes.Underline), "Underline retained.");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ColorStyle", "CellWidths", "Cell factories enforce width invariants",
                        _ =>
                        {
                            Cell wide = Cell.Glyph("世", CellStyle.Default, 2);
                            Check.Equal(2, wide.Width, "Wide width");
                            Check.False(wide.IsContinuation, "Leading cell is not a continuation.");
                            Check.True(Cell.Continuation(CellStyle.Default).IsContinuation, "Continuation flag.");
                            Check.Throws<System.ArgumentException>(
                                () => Cell.Glyph("", CellStyle.Default, 1), "Empty glyph");
                            Check.Throws<System.ArgumentOutOfRangeException>(
                                () => Cell.Glyph("x", CellStyle.Default, 3), "Invalid width");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}

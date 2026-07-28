namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;

    /// <summary>
    /// Touchstone suite covering the fluent <see cref="Text"/> / <see cref="StyledText"/> builder
    /// and its rendering through a surface.
    /// </summary>
    public static class StyledTextSuite
    {
        /// <summary>
        /// Builds the styled-text suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "StyledText",
                displayName: "Styled Text Builder",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("StyledText", "FluentStyle", "Fluent builder applies style to all spans",
                        _ =>
                        {
                            StyledText text = Text.From("hi").Bold().Red();
                            Check.Equal(1, text.Spans.Count, "Single span");
                            CellStyle style = text.Spans[0].Style;
                            Check.True(style.HasAttribute(CellAttributes.Bold), "Bold applied");
                            Check.Equal(Color.FromPalette(1), style.Foreground, "Red foreground");
                            Check.Equal("hi", text.ToPlainString(), "Plain text preserved");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("StyledText", "Append", "Appending preserves per-span styles",
                        _ =>
                        {
                            StyledText combined = Text.From("a").Red().Append(Text.From("b").Blue());
                            Check.Equal(2, combined.Spans.Count, "Two spans");
                            Check.Equal(Color.FromPalette(1), combined.Spans[0].Style.Foreground, "First span red");
                            Check.Equal(Color.FromPalette(4), combined.Spans[1].Style.Foreground, "Second span blue");
                            Check.Equal("ab", combined.ToPlainString(), "Concatenated text");
                            Check.Equal(2, combined.Width, "Combined width");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("StyledText", "RenderToSurface", "Styled text renders with its styles",
                        _ =>
                        {
                            CellBuffer buffer = new CellBuffer(5, 1);
                            BufferSurface surface = new BufferSurface(buffer);
                            StyledText text = Text.From("Hi").Red().Append(Text.From("!").Blue());
                            int advanced = surface.DrawStyledText(0, 0, text);
                            Check.Equal(3, advanced, "Columns advanced");
                            Check.Equal("H", buffer.Get(0, 0).Grapheme, "First glyph");
                            Check.Equal(Color.FromPalette(1), buffer.Get(0, 0).Style.Foreground, "First glyph red");
                            Check.Equal(Color.FromPalette(4), buffer.Get(2, 0).Style.Foreground, "Third glyph blue");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}

namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Theming;

    /// <summary>
    /// Touchstone suite covering the inline markup parser (10.4) and the expanded theme role
    /// vocabulary plus <see cref="StyledText.Style"/> (10.16).
    /// </summary>
    public static class MarkupThemeSuite
    {
        /// <summary>
        /// Builds the markup and theme suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "MarkupTheme",
                displayName: "Markup and Theme Roles",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("MarkupTheme", "Attributes", "Markup applies attributes",
                        _ =>
                        {
                            StyledText t = Markup.Parse("[bold]x[/]");
                            Check.Equal(1, t.Spans.Count, "One span");
                            Check.Equal("x", t.Spans[0].Text, "Text");
                            Check.True(t.Spans[0].Style.HasAttribute(CellAttributes.Bold), "Bold applied");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MarkupTheme", "Colors", "Named, hex, and background colors parse",
                        _ =>
                        {
                            Check.Equal(Color.FromPalette(1), Markup.Parse("[red]x[/]").Spans[0].Style.Foreground, "Named red");
                            Check.Equal(Color.FromRgb(0xFF, 0x00, 0x00), Markup.Parse("[#ff0000]x[/]").Spans[0].Style.Foreground, "Hex red");
                            Check.Equal(Color.FromPalette(4), Markup.Parse("[on blue]x[/]").Spans[0].Style.Background, "Background blue");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MarkupTheme", "Nested", "Nested tags combine and close in order",
                        _ =>
                        {
                            StyledText t = Markup.Parse("[bold][red]x[/][/]");
                            CellStyle style = t.Spans[0].Style;
                            Check.True(style.HasAttribute(CellAttributes.Bold), "Bold from outer");
                            Check.Equal(Color.FromPalette(1), style.Foreground, "Red from inner");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MarkupTheme", "MultiSpan", "Text before and after a tag yields multiple spans",
                        _ =>
                        {
                            StyledText t = Markup.Parse("a[red]b[/]c");
                            Check.Equal(3, t.Spans.Count, "Three spans");
                            Check.Equal("abc", t.ToPlainString(), "Plain text preserved");
                            Check.Equal(Color.FromPalette(1), t.Spans[1].Style.Foreground, "Middle span red");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MarkupTheme", "EscapeAndUnterminated", "Escaped brackets and stray tags are literal",
                        _ =>
                        {
                            Check.Equal("[x]", Markup.Parse("[[x]]").ToPlainString(), "Escaped brackets");
                            Check.Equal("[bold x", Markup.Parse("[bold x").ToPlainString(), "Unterminated tag is literal");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MarkupTheme", "NullThrows", "Null markup is rejected",
                        _ =>
                        {
                            Check.Throws<ArgumentNullException>(() => Markup.Parse(null!), "Parse(null)");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MarkupTheme", "ThemeRoles", "Themes expose distinct semantic role colors",
                        _ =>
                        {
                            Theme dark = Theme.Dark;
                            Check.Equal(Color.FromPalette(2), dark.Success.Foreground, "Success is green");
                            Check.Equal(Color.FromPalette(1), dark.Error.Foreground, "Error is red");
                            Check.Equal(Color.FromPalette(3), dark.Warning.Foreground, "Warning is yellow");
                            Check.Equal(Color.FromPalette(6), dark.Info.Foreground, "Info is cyan");
                            Check.Equal(dark.Text.Background, dark.Success.Background, "Roles share the theme background");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MarkupTheme", "StyleApplies", "StyledText.Style applies a role to every span",
                        _ =>
                        {
                            CellStyle role = Theme.Dark.Error;
                            StyledText t = Text.From("a").Append("b").Style(role);
                            Check.Equal(role, t.Spans[0].Style, "First span restyled");
                            Check.Equal(role, t.Spans[1].Style, "Second span restyled");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}

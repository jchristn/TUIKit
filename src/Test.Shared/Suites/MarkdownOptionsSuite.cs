namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Theming;

    /// <summary>
    /// Coverage for the Markdown configuration surface (plan item M3): documented defaults, deep copies, range
    /// and null validation, glyph presets, the ASCII preset, and theme-derived styles.
    /// </summary>
    public static class MarkdownOptionsSuite
    {
        private const string _Id = "MarkdownOptions";

        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: _Id,
                displayName: "Markdown Render Options",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(_Id, "Defaults", "Every option starts at its documented default",
                        _ =>
                        {
                            MarkdownRenderOptions o = new MarkdownRenderOptions();
                            Check.Equal(0, o.MaxWidth, "MaxWidth");
                            Check.Equal(10, o.MinWidth, "MinWidth");
                            Check.Equal(MarkdownOverflow.Wrap, o.Overflow, "Overflow");
                            Check.Equal(MarkdownOverflow.Wrap, o.CodeOverflow, "CodeOverflow");
                            Check.Equal("…", o.Ellipsis, "Ellipsis");
                            Check.Equal(1, o.BlockSpacing, "BlockSpacing");
                            Check.False(o.ShowHeadingMarkers, "ShowHeadingMarkers");
                            Check.Equal("•◦▪", string.Concat(o.BulletGlyphs), "BulletGlyphs");
                            Check.Equal(0, o.ListIndent, "ListIndent");
                            Check.Equal("☐", o.TaskOpenGlyph, "TaskOpenGlyph");
                            Check.Equal("☑", o.TaskDoneGlyph, "TaskDoneGlyph");
                            Check.Equal("│ ", o.QuotePrefix, "QuotePrefix");
                            Check.Equal("─", o.RuleGlyph, "RuleGlyph");
                            Check.Equal(0, o.RuleWidth, "RuleWidth");
                            Check.Equal(2, o.CodeIndent, "CodeIndent");
                            Check.True(o.ShowCodeLanguage, "ShowCodeLanguage");
                            Check.True(o.SyntaxHighlighting, "SyntaxHighlighting");
                            Check.False(o.FillCodeBackground, "FillCodeBackground");
                            Check.True(o.ShowLinkUrls, "ShowLinkUrls");

                            MarkdownTableOptions t = o.Table;
                            Check.Equal("┌", t.Glyphs.TopLeft, "Glyphs");
                            Check.True(t.ShowOuterBorder, "ShowOuterBorder");
                            Check.True(t.ShowHeaderSeparator, "ShowHeaderSeparator");
                            Check.False(t.ShowRowSeparators, "ShowRowSeparators");
                            Check.Equal(1, t.CellPadding, "CellPadding");
                            Check.Equal(3, t.MinColumnWidth, "MinColumnWidth");
                            Check.Equal(0, t.MaxColumnWidth, "MaxColumnWidth");
                            Check.Equal(4, t.MaxRowLines, "MaxRowLines");
                            Check.Equal(MarkdownOverflow.Wrap, t.CellOverflow, "CellOverflow");
                            Check.Equal(MarkdownTableFallback.Stack, t.Fallback, "Fallback");
                            Check.False(t.Expand, "Expand");

                            MarkdownStyles s = o.Styles;
                            Check.True(s.Strong.HasAttribute(CellAttributes.Bold), "Strong");
                            Check.True(s.Emphasis.HasAttribute(CellAttributes.Italic), "Emphasis");
                            Check.True(s.Strikethrough.HasAttribute(CellAttributes.Strikethrough), "Strikethrough");
                            Check.True(s.Link.HasAttribute(CellAttributes.Underline), "Link");
                            Check.Equal(Color.FromPalette(6), s.TableHeader.Foreground, "TableHeader");
                            Check.Equal(CellStyle.Default, s.TableFirstColumn, "TableFirstColumn");
                            Check.Equal(CellStyle.Default, s.TableCell, "TableCell");
                            Check.Equal(Color.FromPalette(8), s.TableBorder.Foreground, "TableBorder");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "CloneIsDeep", "Clone copies styles and table options instead of sharing them",
                        _ =>
                        {
                            MarkdownRenderOptions original = new MarkdownRenderOptions();
                            original.MaxWidth = 50;
                            original.Table.CellPadding = 2;
                            MarkdownRenderOptions copy = original.Clone();
                            copy.Styles.TableHeader = CellStyle.Default;
                            copy.Table.CellPadding = 0;
                            copy.MaxWidth = 10;
                            Check.Equal(Color.FromPalette(6), original.Styles.TableHeader.Foreground, "styles not shared");
                            Check.Equal(2, original.Table.CellPadding, "table not shared");
                            Check.Equal(50, original.MaxWidth, "scalars not shared");
                            Check.Equal(2, original.Clone().Table.CellPadding, "values copied");
                            MarkdownStyles styles = MarkdownStyles.Default;
                            MarkdownStyles stylesCopy = styles.Clone();
                            stylesCopy.Link = CellStyle.Default;
                            Check.True(styles.Link.HasAttribute(CellAttributes.Underline), "styles clone independent");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Ranges", "Every ranged option rejects values just outside its range and accepts its bounds",
                        _ =>
                        {
                            MarkdownRenderOptions o = new MarkdownRenderOptions();
                            Check.Throws<ArgumentOutOfRangeException>(() => o.MaxWidth = -1, "MaxWidth -1");
                            Check.Throws<ArgumentOutOfRangeException>(() => o.MinWidth = 0, "MinWidth 0");
                            Check.Throws<ArgumentOutOfRangeException>(() => o.MinWidth = 1001, "MinWidth 1001");
                            Check.Throws<ArgumentOutOfRangeException>(() => o.BlockSpacing = -1, "BlockSpacing -1");
                            Check.Throws<ArgumentOutOfRangeException>(() => o.BlockSpacing = 5, "BlockSpacing 5");
                            Check.Throws<ArgumentOutOfRangeException>(() => o.ListIndent = -1, "ListIndent -1");
                            Check.Throws<ArgumentOutOfRangeException>(() => o.ListIndent = 9, "ListIndent 9");
                            Check.Throws<ArgumentOutOfRangeException>(() => o.RuleWidth = -1, "RuleWidth -1");
                            Check.Throws<ArgumentOutOfRangeException>(() => o.CodeIndent = -1, "CodeIndent -1");
                            Check.Throws<ArgumentOutOfRangeException>(() => o.CodeIndent = 17, "CodeIndent 17");
                            Check.Throws<ArgumentOutOfRangeException>(() => o.Table.CellPadding = -1, "CellPadding -1");
                            Check.Throws<ArgumentOutOfRangeException>(() => o.Table.CellPadding = 5, "CellPadding 5");
                            Check.Throws<ArgumentOutOfRangeException>(() => o.Table.MinColumnWidth = 0, "MinColumnWidth 0");
                            Check.Throws<ArgumentOutOfRangeException>(() => o.Table.MinColumnWidth = 1001, "MinColumnWidth 1001");
                            Check.Throws<ArgumentOutOfRangeException>(() => o.Table.MaxColumnWidth = -1, "MaxColumnWidth -1");

                            o.MaxWidth = 0;
                            o.MinWidth = 1;
                            o.MinWidth = 1000;
                            o.BlockSpacing = 0;
                            o.BlockSpacing = 4;
                            o.ListIndent = 8;
                            o.CodeIndent = 16;
                            o.Table.CellPadding = 0;
                            o.Table.CellPadding = 4;
                            o.Table.MinColumnWidth = 1;
                            o.Table.MinColumnWidth = 1000;
                            Check.Equal(1000, o.Table.MinColumnWidth, "bounds accepted");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Nulls", "Null and empty values are rejected with the documented exceptions",
                        _ =>
                        {
                            MarkdownRenderOptions o = new MarkdownRenderOptions();
                            Check.Throws<ArgumentNullException>(() => o.Styles = null!, "Styles");
                            Check.Throws<ArgumentNullException>(() => o.Table = null!, "Table");
                            Check.Throws<ArgumentNullException>(() => o.Ellipsis = null!, "Ellipsis");
                            Check.Throws<ArgumentNullException>(() => o.BulletGlyphs = null!, "BulletGlyphs");
                            Check.Throws<ArgumentNullException>(() => o.Table.Glyphs = null!, "Glyphs");
                            Check.Throws<ArgumentNullException>(() => o.TaskOpenGlyph = null!, "TaskOpenGlyph null");
                            Check.Throws<ArgumentNullException>(() => o.QuotePrefix = null!, "QuotePrefix null");
                            Check.Throws<ArgumentException>(() => o.TaskDoneGlyph = string.Empty, "TaskDoneGlyph empty");
                            Check.Throws<ArgumentException>(() => o.RuleGlyph = string.Empty, "RuleGlyph empty");
                            Check.Throws<ArgumentException>(() => o.BulletGlyphs = new string[0], "BulletGlyphs empty");
                            Check.Throws<ArgumentException>(() => o.BulletGlyphs = new[] { "-", null! }, "BulletGlyphs null entry");
                            Check.Throws<ArgumentException>(() => o.BulletGlyphs = new[] { "-", string.Empty }, "BulletGlyphs empty entry");
                            o.Ellipsis = string.Empty;
                            Check.Equal(string.Empty, o.Ellipsis, "empty ellipsis allowed");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "BulletsCopied", "BulletGlyphs copies the list it is given",
                        _ =>
                        {
                            MarkdownRenderOptions o = new MarkdownRenderOptions();
                            string[] glyphs = new[] { ">" };
                            o.BulletGlyphs = glyphs;
                            glyphs[0] = "?";
                            Check.Equal("> a", MarkdownProbe.Plain("- a", 20, o)[0], "copy used");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Glyphs", "Glyph presets, BorderStyle mapping, and glyph validation",
                        _ =>
                        {
                            Check.Equal("╭", MarkdownTableGlyphs.Rounded.TopLeft, "Rounded");
                            Check.Equal("┏", MarkdownTableGlyphs.Heavy.TopLeft, "Heavy");
                            Check.Equal("╔", MarkdownTableGlyphs.Double.TopLeft, "Double");
                            Check.Equal("+", MarkdownTableGlyphs.Ascii.Cross, "Ascii");
                            Check.Equal(" ", MarkdownTableGlyphs.None.Vertical, "None");
                            Check.Equal("┌", MarkdownTableGlyphs.FromBorderStyle(BorderStyle.Line).TopLeft, "Line");
                            Check.Equal("╭", MarkdownTableGlyphs.FromBorderStyle(BorderStyle.Rounded).TopLeft, "Rounded map");
                            Check.Equal("┏", MarkdownTableGlyphs.FromBorderStyle(BorderStyle.Thick).TopLeft, "Thick map");
                            Check.Equal("╔", MarkdownTableGlyphs.FromBorderStyle(BorderStyle.Double).TopLeft, "Double map");
                            Check.Equal("+", MarkdownTableGlyphs.FromBorderStyle(BorderStyle.Ascii).TopLeft, "Ascii map");
                            Check.Equal("+", MarkdownTableGlyphs.FromBorderStyle(BorderStyle.AsciiHeavy).TopLeft, "AsciiHeavy map");
                            Check.Equal(" ", MarkdownTableGlyphs.FromBorderStyle(BorderStyle.None).TopLeft, "None map");
                            Check.Equal("┌", MarkdownTableGlyphs.FromBorderStyle((BorderStyle)99).TopLeft, "unknown maps to Light");

                            MarkdownTableGlyphs custom = new MarkdownTableGlyphs("=", "!", "1", "2", "3", "4", "5", "6", "7", "8", "9");
                            Check.Equal("5", custom.Cross, "custom glyphs kept");
                            Check.Throws<ArgumentNullException>(() => new MarkdownTableGlyphs(null!, "|", "+", "+", "+", "+", "+", "+", "+", "+", "+"), "null glyph");
                            Check.Throws<ArgumentException>(() => new MarkdownTableGlyphs("", "|", "+", "+", "+", "+", "+", "+", "+", "+", "+"), "empty glyph");
                            Check.Throws<ArgumentException>(() => new MarkdownTableGlyphs("--", "|", "+", "+", "+", "+", "+", "+", "+", "+", "+"), "two-column glyph");
                            Check.Throws<ArgumentException>(() => new MarkdownTableGlyphs("-", "中", "+", "+", "+", "+", "+", "+", "+", "+", "+"), "wide glyph");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "AsciiPreset", "The ASCII preset renders every construct without non-ASCII characters",
                        _ =>
                        {
                            string md = "# T\n\n- a\n  - b\n- [ ] c\n- [x] d\n\n> q\n\n---\n\n| x | y |\n|---|---|\n| 1 | a long cell that will need truncation |";
                            MarkdownRenderOptions o = MarkdownRenderOptions.Ascii();
                            o.Table.CellOverflow = MarkdownOverflow.Truncate;
                            string text = MarkdownProbe.Join(MarkdownProbe.Plain(md, 30, o));
                            foreach (char c in text)
                                Check.True(c < 128, "non-ASCII character U+" + ((int)c).ToString("X4") + " in: " + text);

                            Check.True(text.Contains("[ ] c") && text.Contains("[x] d"), "ascii tasks");
                            Check.True(text.Contains("+---"), "ascii table");
                            Check.True(text.Contains("..."), "ascii ellipsis");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "FromTheme", "Theme-derived styles cover the new table and link styles",
                        _ =>
                        {
                            Theme theme = Theme.Light;
                            MarkdownStyles styles = MarkdownStyles.FromTheme(theme);
                            Check.Equal(theme.Border, styles.TableBorder, "border");
                            Check.Equal(theme.Accent.Foreground, styles.TableHeader.Foreground, "header color");
                            Check.True(styles.TableHeader.HasAttribute(CellAttributes.Bold), "header bold");
                            Check.True(styles.Link.HasAttribute(CellAttributes.Underline), "link underlined");
                            Check.Equal(theme.Muted, styles.LinkUrl, "url muted");
                            Check.Throws<ArgumentNullException>(() => MarkdownStyles.FromTheme(null!), "null theme");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}

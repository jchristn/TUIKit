namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Testing;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for 1.7 item V2: <see cref="MarkdownStyles.FromTheme(Theme, bool)"/>,
    /// <see cref="MarkdownRenderOptions.WithTheme"/>, and <see cref="MarkdownView.UseThemeStyles"/>.
    /// </summary>
    public static class MarkdownThemeSuite
    {
        private const string _Id = "MarkdownTheme";

        private const string _Document = "# Title\n\n- one\n- [ ] task\n\n> quoted\n\n---\n\n| a | b |\n|---|---|\n| 1 | 2 |\n";

        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: _Id,
                displayName: "Markdown Theme",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(_Id, "NoBackgrounds", "includeBackgrounds false leaves every style's background at default on the built-in themes",
                        _ =>
                        {
                            foreach (Theme theme in new[] { Theme.Dark, Theme.Light, Theme.HighContrast })
                            {
                                MarkdownStyles bare = MarkdownStyles.FromTheme(theme, false);
                                MarkdownStyles full = MarkdownStyles.FromTheme(theme, true);
                                MarkdownStyles legacy = MarkdownStyles.FromTheme(theme);
                                bool anyBackground = false;
                                foreach (PropertyInfo property in StyleProperties())
                                {
                                    CellStyle style = (CellStyle)property.GetValue(bare)!;
                                    CellStyle withBackground = (CellStyle)property.GetValue(full)!;
                                    Check.Equal(Color.Default, style.Background, theme.Name + " " + property.Name + " background");
                                    Check.Equal(withBackground.Foreground, style.Foreground, theme.Name + " " + property.Name + " foreground kept");
                                    Check.Equal(withBackground.Attributes, style.Attributes, theme.Name + " " + property.Name + " attributes kept");
                                    Check.Equal(withBackground, (CellStyle)property.GetValue(legacy)!, theme.Name + " " + property.Name + " one-argument overload unchanged");
                                    anyBackground |= withBackground.Background != Color.Default;
                                }

                                Check.True(anyBackground, theme.Name + " carries backgrounds when they are included");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "AsciiTheme", "WithTheme on an ASCII theme renders a table, list, task, quote, and rule with no character above 127",
                        _ =>
                        {
                            MarkdownRenderOptions options = new MarkdownRenderOptions();
                            Check.False(IsAscii(Plain(_Document, 40, options)), "the default options use Unicode glyphs");
                            MarkdownRenderOptions themed = options.WithTheme(Theme.HighContrast);
                            Check.True(IsAscii(Plain(_Document, 40, themed)), "the ASCII theme reaches every glyph");
                            Check.Equal("...", themed.Ellipsis, "ellipsis");
                            Check.True(IsAscii(Plain(_Document, 40, options.WithTheme(Theme.Dark))) == false, "a Unicode theme keeps the glyphs");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "KeepsLayout", "WithTheme keeps layout settings and never mutates the receiver",
                        _ =>
                        {
                            MarkdownRenderOptions options = new MarkdownRenderOptions();
                            options.MaxWidth = 40;
                            options.Table.Fallback = MarkdownTableFallback.Clip;
                            options.BlockSpacing = 2;
                            CellStyle before = options.Styles.Heading1;
                            MarkdownRenderOptions themed = options.WithTheme(Theme.Light);
                            Check.False(ReferenceEquals(options, themed), "a new instance");
                            Check.Equal(40, themed.MaxWidth, "MaxWidth kept");
                            Check.Equal(MarkdownTableFallback.Clip, themed.Table.Fallback, "table fallback kept");
                            Check.Equal(2, themed.BlockSpacing, "spacing kept");
                            Check.Equal(before, options.Styles.Heading1, "receiver styles untouched");
                            Check.Equal(MarkdownStyles.FromTheme(Theme.Light, false).Heading1, themed.Styles.Heading1, "themed heading");

                            CellStyle key = CellStyle.Default.WithForeground(Color.FromPalette(3)).WithAttribute(CellAttributes.Bold, true);
                            options.Styles.TableFirstColumn = key;
                            options.Styles.Strong = key;
                            MarkdownRenderOptions keyed = options.WithTheme(Theme.Dark);
                            Check.Equal(key, keyed.Styles.TableFirstColumn, "a style the theme does not define is kept");
                            Check.Equal(key, keyed.Styles.Strong, "strong is kept");

                            MarkdownRenderOptions ascii = options.WithTheme(Theme.HighContrast);
                            Check.Equal("•", options.BulletGlyphs[0], "receiver glyphs untouched by an ASCII theme");
                            Check.Equal("-", ascii.BulletGlyphs[0], "ASCII bullet");
                            Check.Equal("-", ascii.WithTheme(Theme.Dark).BulletGlyphs[0], "not reversible: a later Unicode theme keeps the receiver's glyphs");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "ViewOffByDefault", "A view with UseThemeStyles false renders identically before and after ThemeApplier runs",
                        _ =>
                        {
                            MarkdownView view = new MarkdownView(_Document);
                            CellBuffer before = Draw(view, 40, 16);
                            Check.True(ThemeApplier.Apply(view, Theme.Dark), "the view is themeable");
                            CellBuffer after = Draw(view, 40, 16);
                            Check.True(SameCells(before, after), "cells unchanged while UseThemeStyles is false");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "ViewFollowsTheme", "Setting UseThemeStyles restyles at once from the recorded theme, and clearing it restores the base",
                        _ =>
                        {
                            MarkdownView view = new MarkdownView(_Document);
                            view.Background = CellStyle.Default.WithBackground(Color.FromRgb(1, 2, 3));
                            CellBuffer plain = Draw(view, 40, 16);
                            view.ApplyTheme(Theme.Light);
                            view.UseThemeStyles = true;
                            CellBuffer themed = Draw(view, 40, 16);
                            Check.False(SameCells(plain, themed), "restyled without another ApplyTheme");
                            Check.Equal(MarkdownStyles.FromTheme(Theme.Light, false).Heading1.Foreground, themed.Get(0, 0).Style.Foreground, "heading uses the theme color");
                            Check.Equal(Color.FromRgb(1, 2, 3), themed.Get(0, 0).Style.Background, "the caller's background shows through");
                            Check.Equal(Color.FromRgb(1, 2, 3), view.Background.Background, "Background is never changed by the theme");

                            view.UseThemeStyles = false;
                            Check.True(SameCells(plain, Draw(view, 40, 16)), "clearing it returns to the base options");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "AsciiThenUnicode", "Applying an ASCII theme and then Dark restores the base glyphs; Options returns the base instance",
                        _ =>
                        {
                            MarkdownView view = new MarkdownView(_Document) { UseThemeStyles = true };
                            MarkdownRenderOptions baseOptions = view.Options;
                            view.ApplyTheme(Theme.HighContrast);
                            string ascii = Snapshot.RenderWidget(view, 40, 16);
                            Check.True(IsAscii(ascii), "ASCII while the ASCII theme is applied");
                            Check.True(ReferenceEquals(baseOptions, view.Options), "Options still returns the base instance");
                            Check.Equal("•", view.Options.BulletGlyphs[0], "the base keeps its glyphs");

                            view.ApplyTheme(Theme.Dark);
                            string unicode = Snapshot.RenderWidget(view, 40, 16);
                            Check.True(unicode.Contains("┌") && unicode.Contains("•"), "Unicode glyphs restored");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "NullTheme", "A null theme throws ArgumentNullException everywhere",
                        _ =>
                        {
                            Check.Throws<ArgumentNullException>(() => MarkdownStyles.FromTheme(null!, false), "FromTheme");
                            Check.Throws<ArgumentNullException>(() => new MarkdownRenderOptions().WithTheme(null!), "WithTheme");
                            Check.Throws<ArgumentNullException>(() => new MarkdownView().ApplyTheme(null!), "ApplyTheme");
                            return Task.CompletedTask;
                        }),
                });
        }

        internal static CellBuffer Draw(IWidget widget, int width, int height)
        {
            CellBuffer buffer = new CellBuffer(width, height);
            widget.Render(new BufferSurface(buffer));
            return buffer;
        }

        internal static bool SameCells(CellBuffer a, CellBuffer b)
        {
            if (a.Width != b.Width || a.Height != b.Height)
                return false;

            for (int y = 0; y < a.Height; y++)
            {
                for (int x = 0; x < a.Width; x++)
                {
                    if (!a.Get(x, y).Equals(b.Get(x, y)))
                        return false;
                }
            }

            return true;
        }

        private static IEnumerable<PropertyInfo> StyleProperties()
        {
            foreach (PropertyInfo property in typeof(MarkdownStyles).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.PropertyType == typeof(CellStyle))
                    yield return property;
            }
        }

        private static string Plain(string markdown, int width, MarkdownRenderOptions options)
        {
            List<string> lines = new List<string>();
            foreach (StyledText row in MarkdownRenderer.Render(markdown, width, options))
                lines.Add(row.ToPlainString());

            return string.Join("\n", lines);
        }

        private static bool IsAscii(string text)
        {
            foreach (char c in text)
            {
                if (c > 127)
                    return false;
            }

            return true;
        }
    }
}

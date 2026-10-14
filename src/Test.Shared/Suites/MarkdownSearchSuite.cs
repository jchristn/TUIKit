namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Input;
    using TUIKit.Testing;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for 1.7 item V4: <see cref="MarkdownView"/> search, F3 keys, <see cref="Theme.SearchMatchRole"/>,
    /// <see cref="Pane.SearchText"/>, and <see cref="Pane"/> output that stays identical to 1.6.
    /// </summary>
    public static class MarkdownSearchSuite
    {
        private const string _Id = "MarkdownSearch";

        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: _Id,
                displayName: "Markdown Search",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(_Id, "FindsEveryBlockKind", "A match in a list item, a table cell, and a code block is found and scrolled into view",
                        _ =>
                        {
                            MarkdownView view = new MarkdownView(Sections());
                            Snapshot.RenderWidget(view, 40, 4);
                            view.SetSearch("needle");
                            Check.Equal("needle", view.SearchText, "SearchText");
                            List<int> offsets = new List<int>();
                            for (int i = 0; i < 3; i++)
                            {
                                Check.True(view.FindNext(), "match " + i);
                                offsets.Add(view.ScrollOffset);
                                string screen = Snapshot.RenderWidget(view, 40, 4);
                                Check.True(screen.Contains("needle"), "match " + i + " is visible");
                            }

                            Check.True(offsets[0] < offsets[1] && offsets[1] < offsets[2], "three distinct matches in order");
                            Check.True(Snapshot.RenderWidget(view, 40, 4).Contains("code needle"), "the last match is in the code block");
                            Check.True(view.FindNext(), "wraps around");
                            Check.Equal(offsets[0], view.ScrollOffset, "back to the first match");
                            Check.True(view.FindPrevious(), "previous wraps backward");
                            Check.Equal(offsets[2], view.ScrollOffset, "to the last match");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "MatchesWhatIsShown", "Matching ignores case and Markdown markup",
                        _ =>
                        {
                            MarkdownView view = new MarkdownView(Filler(10) + "Some **bold** words.\n");
                            Snapshot.RenderWidget(view, 40, 4);
                            view.SetSearch("**bold**");
                            Check.False(view.FindNext(), "Markdown syntax is not searched");
                            view.SetSearch("BOLD");
                            Check.True(view.FindNext(), "case-insensitive over the rendered text");
                            CellBuffer buffer = MarkdownThemeSuite.Draw(view, 40, 4);
                            Check.True(HasStyle(buffer, Default()), "the match is highlighted");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Clearing", "Clearing the search removes highlights and makes FindNext return false",
                        _ =>
                        {
                            MarkdownView view = new MarkdownView("needle here\n");
                            view.SetSearch("needle");
                            Check.True(HasStyle(MarkdownThemeSuite.Draw(view, 30, 3), Default()), "highlighted");
                            view.SetSearch(null);
                            Check.True(view.SearchText == null, "SearchText cleared");
                            Check.False(view.FindNext(), "FindNext with no search");
                            Check.False(view.FindPrevious(), "FindPrevious with no search");
                            Check.False(HasStyle(MarkdownThemeSuite.Draw(view, 30, 3), Default()), "no highlight after clearing");
                            view.SetSearch(string.Empty);
                            Check.True(view.SearchText == null, "empty clears too");
                            view.SetSearch("absent");
                            Check.False(view.FindNext(), "no match returns false");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "F3Keys", "F3 and Shift+F3 step through matches while a search is set, and are not consumed otherwise",
                        _ =>
                        {
                            MarkdownView view = new MarkdownView(Sections());
                            Snapshot.RenderWidget(view, 40, 4);
                            Check.False(view.HandleKey(KeyEvent.Special(KeyCode.F3)), "F3 without a search is left to the app");
                            view.SetSearch("needle");
                            Check.True(view.HandleKey(KeyEvent.Special(KeyCode.F3)), "F3 consumed");
                            int first = view.ScrollOffset;
                            Check.True(view.HandleKey(KeyEvent.Special(KeyCode.F3)), "F3 again");
                            Check.True(view.ScrollOffset > first, "moved to the next match");
                            Check.True(view.HandleKey(KeyEvent.Special(KeyCode.F3, KeyModifiers.Shift)), "Shift+F3 consumed");
                            Check.Equal(first, view.ScrollOffset, "back to the previous match");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "FromScrolledView", "FindNext from a scrolled view finds the first match below the top row",
                        _ =>
                        {
                            MarkdownView view = new MarkdownView(Sections());
                            Snapshot.RenderWidget(view, 40, 4);
                            view.SetSearch("needle");
                            view.FindNext();
                            int first = view.ScrollOffset;
                            view.FindNext();
                            int second = view.ScrollOffset;
                            view.SetSearch("needle");
                            view.ScrollOffset = first + 1;
                            Check.True(view.FindNext(), "found");
                            Check.Equal(second, view.ScrollOffset, "the search started below the top row, not at the document start");

                            MarkdownView unrendered = new MarkdownView(Sections());
                            unrendered.SetSearch("needle");
                            Check.True(unrendered.FindNext(), "a view that was never rendered searches at 80 columns");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "ViewTheme", "The view's highlight follows Theme.SearchMatchRole",
                        _ =>
                        {
                            CellStyle red = CellStyle.Default.WithForeground(Color.FromRgb(255, 0, 0));
                            Theme custom = new Theme("custom", CellStyle.Default, CellStyle.Default, CellStyle.Default, CellStyle.Default);
                            custom.SetStyle(Theme.SearchMatchRole, red);
                            MarkdownView view = new MarkdownView("needle\n");
                            view.SetSearch("needle");
                            view.ApplyTheme(custom);
                            Check.True(HasStyle(MarkdownThemeSuite.Draw(view, 20, 2), red), "registered role used");
                            foreach (Theme theme in new[] { Theme.Dark, Theme.Light, Theme.HighContrast })
                                Check.True(theme.HasStyle(Theme.SearchMatchRole), theme.Name + " registers the role");

                            Check.True(Theme.HighContrast.GetStyle(Theme.SearchMatchRole).HasAttribute(CellAttributes.Underline), "HighContrast underlines matches");
                            Check.Equal(Default(), Theme.Dark.GetStyle(Theme.SearchMatchRole), "Dark registers the 1.6 style");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "PaneUnchanged", "Pane output is cell-for-cell identical to 1.6 except the HighContrast underline",
                        _ =>
                        {
                            CellBuffer baseline = DrawPane(null);
                            Check.True(HasStyle(baseline, Default()), "the 1.6 match style: black on palette yellow");
                            Theme custom = new Theme("custom", CellStyle.Default, CellStyle.Default, CellStyle.Default, CellStyle.Default);
                            foreach (Theme theme in new[] { Theme.Dark, Theme.Light, custom })
                                Check.True(MarkdownThemeSuite.SameCells(baseline, DrawPane(theme)), theme.Name + " renders identically");

                            CellBuffer contrast = DrawPane(Theme.HighContrast);
                            Check.False(MarkdownThemeSuite.SameCells(baseline, contrast), "HighContrast differs");
                            for (int y = 0; y < baseline.Height; y++)
                            {
                                for (int x = 0; x < baseline.Width; x++)
                                {
                                    Cell a = baseline.Get(x, y);
                                    Cell b = contrast.Get(x, y);
                                    Check.Equal(a.Grapheme, b.Grapheme, "same text at " + x + "," + y);
                                    CellStyle expected = a.Style == Default() ? a.Style.WithAttribute(CellAttributes.Underline, true) : a.Style;
                                    Check.Equal(expected, b.Style, "only matches gain an underline at " + x + "," + y);
                                }
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "PaneSearchText", "Pane.SearchText reports the term and is safe to read while the pane renders on another thread",
                        _ =>
                        {
                            Pane pane = new Pane("p");
                            Check.True(pane.SearchText == null, "null by default");
                            pane.SetSearch("x");
                            Check.Equal("x", pane.SearchText, "set");
                            for (int i = 0; i < 200; i++)
                                pane.WriteLine("line x " + i);

                            int stop = 0;
                            Thread renderer = new Thread(() =>
                            {
                                while (Interlocked.CompareExchange(ref stop, 0, 0) == 0)
                                {
                                    Snapshot.RenderWidget(pane, 30, 5);
                                    pane.SetSearch("line");
                                }
                            });
                            renderer.Start();
                            for (int i = 0; i < 2000; i++)
                            {
                                string? text = pane.SearchText;
                                Check.True(text == null || text == "x" || text == "line", "a consistent value");
                            }

                            Interlocked.Exchange(ref stop, 1);
                            renderer.Join();
                            pane.SetSearch(null);
                            Check.True(pane.SearchText == null, "cleared");
                            Check.Throws<ArgumentNullException>(() => pane.ApplyTheme(null!), "null theme");
                            return Task.CompletedTask;
                        }),
                });
        }

        private static CellStyle Default()
        {
            return CellStyle.Default.WithForeground(Color.FromRgb(0, 0, 0)).WithBackground(Color.FromPalette(3));
        }

        private static string Filler(int lines)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < lines; i++)
                builder.Append("filler paragraph ").Append(i).Append("\n\n");

            return builder.ToString();
        }

        private static string Sections()
        {
            return Filler(6) + "- a list needle item\n\n" + Filler(6) + "| k | v |\n|---|---|\n| needle | x |\n\n" + Filler(6) + "```\ncode needle\n```\n\n" + Filler(6);
        }

        private static CellBuffer DrawPane(Theme? theme)
        {
            Pane pane = new Pane("p");
            pane.WriteLine("first needle line");
            pane.WriteLine("nothing here");
            pane.WriteLine("NEEDLE twice needle");
            pane.SetSearch("needle");
            if (theme != null)
                ThemeApplier.Apply(pane, theme);

            return MarkdownThemeSuite.Draw(pane, 30, 4);
        }

        private static bool HasStyle(CellBuffer buffer, CellStyle style)
        {
            for (int y = 0; y < buffer.Height; y++)
            {
                for (int x = 0; x < buffer.Width; x++)
                {
                    if (buffer.Get(x, y).Style == style)
                        return true;
                }
            }

            return false;
        }
    }
}

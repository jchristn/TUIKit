namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit.Content;
    using TUIKit.Input;
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for the small horizontal utilities: <see cref="HintText"/>, <see cref="ColumnFormatter"/>,
    /// the <see cref="Rule"/> widget, and <see cref="SubmitKeyResolver"/>.
    /// </summary>
    public static class UtilitiesSuite
    {
        /// <summary>
        /// Builds the utilities suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Utilities",
                displayName: "Utilities (HintText / ColumnFormatter / Rule / SubmitKeyResolver)",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Utilities", "HintWraps", "HintText wraps at the width without splitting a segment",
                        _ =>
                        {
                            IReadOnlyList<string> lines = HintText.Wrap("Enter: ok · Esc: cancel · Tab: next", 20);
                            Check.True(lines.Count >= 2, "wrapped to multiple lines");
                            foreach (string line in lines)
                                Check.True(line.Length <= 20, "line within width");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Utilities", "HintLongSegment", "A single oversized segment gets its own line unbroken",
                        _ =>
                        {
                            IReadOnlyList<string> lines = HintText.Wrap("supercalifragilistic · x", 8);
                            Check.Equal(2, lines.Count, "two lines");
                            Check.Equal("supercalifragilistic", lines[0], "long segment intact");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Utilities", "HintGuards", "HintText rejects null text and non-positive width",
                        _ =>
                        {
                            Check.Throws<ArgumentNullException>(() => HintText.Wrap(null!, 10), "null hint");
                            Check.Throws<ArgumentNullException>(() => HintText.Wrap("a", 10, null!), "null separator");
                            Check.Throws<ArgumentOutOfRangeException>(() => HintText.Wrap("a", 0), "zero width");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Utilities", "ColumnsAlign", "ColumnFormatter aligns columns to the widest cell",
                        _ =>
                        {
                            List<IReadOnlyList<string>> rows = new List<IReadOnlyList<string>>
                            {
                                new List<string> { "Open", "Ctrl+O", "/open" },
                                new List<string> { "Quit", "Ctrl+Q", "/quit" }
                            };
                            IReadOnlyList<string> lines = ColumnFormatter.Format(rows);
                            int firstGap = lines[0].IndexOf("Ctrl+O", StringComparison.Ordinal);
                            int secondGap = lines[1].IndexOf("Ctrl+Q", StringComparison.Ordinal);
                            Check.Equal(firstGap, secondGap, "second column starts at the same offset");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Utilities", "ColumnsRagged", "Short rows are padded and formatting does not throw",
                        _ =>
                        {
                            List<IReadOnlyList<string>> rows = new List<IReadOnlyList<string>>
                            {
                                new List<string> { "a", "bb", "ccc" },
                                new List<string> { "d" }
                            };
                            IReadOnlyList<string> lines = ColumnFormatter.Format(rows, 1);
                            Check.Equal(2, lines.Count, "two lines");
                            Check.Equal("d", lines[1], "short row trimmed");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Utilities", "ColumnsGuards", "ColumnFormatter rejects null rows/cells and negative gap",
                        _ =>
                        {
                            Check.Throws<ArgumentNullException>(() => ColumnFormatter.Format(null!), "null rows");
                            List<IReadOnlyList<string>> withNullRow = new List<IReadOnlyList<string>> { null! };
                            Check.Throws<ArgumentNullException>(() => ColumnFormatter.Format(withNullRow), "null row");
                            List<IReadOnlyList<string>> withNullCell = new List<IReadOnlyList<string>> { new List<string> { null! } };
                            Check.Throws<ArgumentNullException>(() => ColumnFormatter.Format(withNullCell), "null cell");
                            List<IReadOnlyList<string>> ok = new List<IReadOnlyList<string>> { new List<string> { "a" } };
                            Check.Throws<ArgumentOutOfRangeException>(() => ColumnFormatter.Format(ok, -1), "negative gap");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Utilities", "RuleRenders", "A horizontal rule fills its width and centers a caption",
                        _ =>
                        {
                            Rule rule = new Rule();
                            rule.Caption = "Section";
                            string text = TUIKit.Testing.Snapshot.RenderWidget(rule, 30, 1);
                            Check.True(text.Contains("Section"), "caption rendered");
                            Check.True(text.Contains("─"), "line glyph rendered");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Utilities", "RuleGuards", "Rule rejects empty glyphs",
                        _ =>
                        {
                            Check.Throws<ArgumentException>(() => new Rule().Glyph = "", "empty glyph");
                            Check.Throws<ArgumentException>(() => new Rule().VerticalGlyph = "", "empty vertical glyph");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Utilities", "SubmitDefaults", "Enter submits and Ctrl+J / Shift+Enter insert a newline",
                        _ =>
                        {
                            SubmitKeyResolver resolver = new SubmitKeyResolver();
                            Check.Equal(SubmitDecision.Submit, resolver.Resolve(KeyEvent.Special(KeyCode.Enter)), "bare Enter submits");
                            Check.Equal(SubmitDecision.InsertNewline, resolver.Resolve(KeyEvent.Char('j', KeyModifiers.Ctrl)), "Ctrl+J newline");
                            Check.Equal(SubmitDecision.InsertNewline, resolver.Resolve(KeyEvent.Special(KeyCode.Enter, KeyModifiers.Shift)), "Shift+Enter newline");
                            Check.Equal(SubmitDecision.Ignore, resolver.Resolve(KeyEvent.Char('a')), "unrelated key ignored");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Utilities", "SubmitInverted", "Inverting the policy swaps Enter and Ctrl+J",
                        _ =>
                        {
                            SubmitKeyResolver resolver = new SubmitKeyResolver();
                            resolver.EnterSubmits = false;
                            Check.Equal(SubmitDecision.InsertNewline, resolver.Resolve(KeyEvent.Special(KeyCode.Enter)), "Enter newline when inverted");
                            Check.Equal(SubmitDecision.Submit, resolver.Resolve(KeyEvent.Char('j', KeyModifiers.Ctrl)), "Ctrl+J submits when inverted");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}

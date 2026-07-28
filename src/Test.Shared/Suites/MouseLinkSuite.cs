namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Input;

    /// <summary>
    /// Touchstone suite covering the link registry and hit-testing, security-aware URL scanning, and
    /// click-count synthesis.
    /// </summary>
    public static class MouseLinkSuite
    {
        /// <summary>
        /// Builds the mouse and link suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "MouseLink",
                displayName: "Mouse, Links, and Hit Testing",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("MouseLink", "HitTest", "Registry hit-tests link rectangles",
                        _ =>
                        {
                            LinkRegistry registry = new LinkRegistry();
                            Link link = registry.Add("docs", new Rect(5, 0, 10, 1), "https://example.com");
                            registry.AddRect(link, new Rect(0, 1, 4, 1)); // wrapped continuation

                            Check.True(registry.HitTest(6, 0) != null, "Inside first rect");
                            Check.Equal(link.Id, registry.HitTest(6, 0)!.Id, "Same link");
                            Check.True(registry.HitTest(2, 1) != null, "Inside wrapped rect");
                            Check.True(registry.HitTest(0, 0) == null, "Outside all rects");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseLink", "ScanAllowlist", "Only allowlisted schemes auto-linkify",
                        _ =>
                        {
                            LinkScanner scanner = new LinkScanner();
                            IReadOnlyList<LinkMatch> matches = scanner.Scan("visit https://a.com, not file:///etc/passwd here");
                            Check.Equal(1, matches.Count, "Only the https URL matched");
                            Check.Equal("https://a.com", matches[0].Uri, "Trailing comma trimmed");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseLink", "ScanMailto", "mailto is allowed, custom schemes are not",
                        _ =>
                        {
                            LinkScanner scanner = new LinkScanner();
                            IReadOnlyList<LinkMatch> matches = scanner.Scan("mail me at mailto:a@b.com or vscode://open");
                            Check.Equal(1, matches.Count, "Only mailto matched");
                            Check.Equal("mailto:a@b.com", matches[0].Uri, "mailto uri");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseLink", "ClickSynthesis", "Presses synthesize double and triple clicks",
                        _ =>
                        {
                            ClickSynthesizer synth = new ClickSynthesizer();
                            synth.ThresholdMilliseconds = 400;
                            Check.Equal(1, synth.RegisterPress(MouseButton.Left, 3, 3, 0), "Single");
                            Check.Equal(2, synth.RegisterPress(MouseButton.Left, 3, 3, 100), "Double");
                            Check.Equal(3, synth.RegisterPress(MouseButton.Left, 3, 3, 200), "Triple");
                            Check.Equal(1, synth.RegisterPress(MouseButton.Left, 3, 3, 300), "Cycles back to single");

                            Check.Equal(2, synth.RegisterPress(MouseButton.Left, 3, 3, 400), "Continues cycling while rapid");
                            Check.Equal(1, synth.RegisterPress(MouseButton.Left, 9, 9, 450), "Different cell resets");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("MouseLink", "ClickTimeout", "Slow presses do not chain",
                        _ =>
                        {
                            ClickSynthesizer synth = new ClickSynthesizer();
                            synth.ThresholdMilliseconds = 300;
                            Check.Equal(1, synth.RegisterPress(MouseButton.Left, 1, 1, 0), "Single");
                            Check.Equal(1, synth.RegisterPress(MouseButton.Left, 1, 1, 1000), "Too slow, new single");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}

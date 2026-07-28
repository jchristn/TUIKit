namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Content;

    /// <summary>
    /// Touchstone suite covering text selection extraction and OSC 52 clipboard encoding.
    /// </summary>
    public static class SelectionSuite
    {
        /// <summary>
        /// Builds the selection suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Selection",
                displayName: "Selection and Clipboard",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Selection", "SingleLine", "Single-line selection extracts a substring",
                        _ =>
                        {
                            List<string> lines = new List<string> { "hello world" };
                            Selection selection = new Selection();
                            selection.Begin(new Point(0, 0));
                            selection.Extend(new Point(5, 0));
                            Check.Equal("hello", selection.GetText(lines), "First five characters");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Selection", "MultiLine", "Multi-line selection joins with newlines",
                        _ =>
                        {
                            List<string> lines = new List<string> { "abcdef", "ghijkl", "mnopqr" };
                            Selection selection = new Selection();
                            selection.Begin(new Point(2, 0));
                            selection.Extend(new Point(3, 2));
                            Check.Equal("cdef\nghijkl\nmno", selection.GetText(lines), "Spanning three lines");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Selection", "ReversedOrder", "Selection normalizes anchor and caret order",
                        _ =>
                        {
                            List<string> lines = new List<string> { "one", "two" };
                            Selection selection = new Selection();
                            selection.Begin(new Point(1, 1)); // caret starts lower-right
                            selection.Extend(new Point(0, 0)); // then moves up-left
                            Check.Equal("one\nt", selection.GetText(lines), "Ordered regardless of drag direction");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Selection", "Osc52", "Clipboard writer encodes OSC 52",
                        _ =>
                        {
                            string sequence = ClipboardWriter.BuildSequence("hi");
                            string expected = Convert.ToBase64String(Encoding.UTF8.GetBytes("hi"));
                            Check.True(sequence.Contains("]52;c;"), "OSC 52 introducer present");
                            Check.True(sequence.Contains(expected), "Base64 payload present");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}

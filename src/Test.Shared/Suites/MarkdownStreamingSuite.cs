namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Testing;

    /// <summary>
    /// Coverage for <see cref="StreamingTranscript.RenderOptions"/> (plan item M9): chunked streaming matches a
    /// single write, partial table rows are held back, finalize semantics, and the 1.5.0 default.
    /// </summary>
    public static class MarkdownStreamingSuite
    {
        private const string _Id = "MarkdownStreaming";

        private const string _Reply = "Here is the comparison:\n\n| Option | Latency | Notes |\n|---|--:|---|\n| redis | 1.2 | fast but needs a service |\n| lru | 0.3 | in process |\n\n- done\n";

        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: _Id,
                displayName: "Markdown Streaming",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(_Id, "ChunksMatchWhole", "Streaming in small chunks ends identical to writing the whole reply",
                        _ =>
                        {
                            foreach (int chunk in new[] { 1, 3, 7, 64 })
                            {
                                Pane streamed = new Pane("s");
                                StreamingTranscript transcript = new StreamingTranscript(streamed);
                                transcript.RenderOptions = new MarkdownRenderOptions();
                                for (int i = 0; i < _Reply.Length; i += chunk)
                                    transcript.AppendText(_Reply.Substring(i, Math.Min(chunk, _Reply.Length - i)));

                                transcript.FinalizeBlock();
                                Pane whole = new Pane("w");
                                whole.WriteMarkdown(_Reply);
                                Check.Equal(RenderText(whole, 50, 20), RenderText(streamed, 50, 20), "chunk size " + chunk);
                                Check.Equal(1, streamed.LineCount, "one block, chunk size " + chunk);
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "PartialRowHeld", "A half-received table row is held back until its newline",
                        _ =>
                        {
                            Pane pane = new Pane("s");
                            StreamingTranscript transcript = new StreamingTranscript(pane);
                            transcript.RenderOptions = new MarkdownRenderOptions();
                            transcript.AppendText("| head1 | head2 |\n|---|---|\n| cellx | ce");
                            string partial = RenderText(pane, 40, 8);
                            Check.True(partial.Contains("head1"), "header shown");
                            Check.False(partial.Contains("cellx"), "partial row held back");
                            transcript.AppendText("lly |\n");
                            Check.True(RenderText(pane, 40, 8).Contains("│ cellx │ celly │"), "row shown once complete");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Finalize", "Finalize with nothing buffered is a no-op; blocks after finalize are separate",
                        _ =>
                        {
                            Pane pane = new Pane("s");
                            StreamingTranscript transcript = new StreamingTranscript(pane);
                            transcript.RenderOptions = new MarkdownRenderOptions();
                            transcript.FinalizeBlock();
                            Check.Equal(0, pane.LineCount, "nothing written");
                            transcript.AppendText("first");
                            transcript.FinalizeBlock();
                            transcript.AppendText("second");
                            transcript.FinalizeBlock();
                            Check.Equal(2, pane.LineCount, "two blocks");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "NotWhileStreaming", "With live rendering off, finalize still writes one reflowing block",
                        _ =>
                        {
                            Pane pane = new Pane("s");
                            StreamingTranscript transcript = new StreamingTranscript(pane);
                            transcript.RenderOptions = new MarkdownRenderOptions();
                            transcript.RenderMarkdownWhileStreaming = false;
                            transcript.AppendText("| a | b |\n|---|");
                            transcript.AppendText("---|\n| 1 | 2 |");
                            Check.False(RenderText(pane, 40, 8).Contains("┌"), "collapsed live line while streaming");
                            transcript.FinalizeBlock();
                            Check.True(RenderText(pane, 40, 8).Contains("┌"), "table after finalize");
                            Check.Equal(1, pane.LineCount, "one line");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "OptionsCopied", "RenderOptions is copied; the default stays null and line by line",
                        _ =>
                        {
                            Pane pane = new Pane("s");
                            StreamingTranscript transcript = new StreamingTranscript(pane);
                            Check.True(transcript.RenderOptions == null, "null by default");
                            transcript.AppendText("| a | b |\n|---|---|\n| 1 | 2 |\n");
                            transcript.FinalizeBlock();
                            Check.True(pane.LineCount >= 3, "legacy mode writes a line per source line");
                            Check.False(RenderText(pane, 40, 8).Contains("┌"), "legacy table rows");

                            MarkdownRenderOptions o = new MarkdownRenderOptions();
                            transcript.RenderOptions = o;
                            o.Table.Glyphs = MarkdownTableGlyphs.Ascii;
                            Check.Equal("┌", transcript.RenderOptions!.Table.Glyphs.TopLeft, "copied on set");
                            transcript.RenderOptions = null;
                            Check.True(transcript.RenderOptions == null, "can be cleared");
                            return Task.CompletedTask;
                        })
                });
        }

        private static string RenderText(Pane pane, int width, int height)
        {
            CellBuffer buffer = new CellBuffer(width, height);
            pane.Render(new BufferSurface(buffer));
            return Snapshot.ToText(buffer);
        }
    }
}

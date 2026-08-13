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
    /// Coverage for <see cref="StreamingTranscript"/>: streaming text into a block, finalizing it as
    /// Markdown, keyed in-place line updates, and argument guards.
    /// </summary>
    public static class StreamingTranscriptSuite
    {
        /// <summary>
        /// Builds the streaming-transcript suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "StreamingTranscript",
                displayName: "Streaming Transcript",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("StreamingTranscript", "StreamAndFinalize", "Streamed text finalizes as Markdown in the pane",
                        _ =>
                        {
                            Pane pane = new Pane("t");
                            StreamingTranscript transcript = new StreamingTranscript(pane);
                            transcript.AppendText("Hello ");
                            transcript.AppendText("**world**");
                            transcript.FinalizeBlock();

                            CellBuffer buffer = new CellBuffer(40, 6);
                            pane.Render(new BufferSurface(buffer));
                            string text = Snapshot.ToText(buffer);
                            Check.True(text.Contains("world"), "finalized text rendered");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("StreamingTranscript", "TrackedUpdate", "A tracked line updates in place rather than appending",
                        _ =>
                        {
                            Pane pane = new Pane("t");
                            StreamingTranscript transcript = new StreamingTranscript(pane);
                            transcript.Track("build");
                            transcript.Update("build", "build: running");
                            transcript.Update("build", "build: done");

                            CellBuffer buffer = new CellBuffer(40, 6);
                            pane.Render(new BufferSurface(buffer));
                            string text = Snapshot.ToText(buffer);
                            Check.True(text.Contains("build: done"), "final state present");
                            Check.False(text.Contains("build: running"), "prior state replaced");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("StreamingTranscript", "FinalizeEmpty", "Finalizing with nothing buffered is safe",
                        _ =>
                        {
                            Pane pane = new Pane("t");
                            StreamingTranscript transcript = new StreamingTranscript(pane);
                            transcript.FinalizeBlock();
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("StreamingTranscript", "Guards", "Constructor and mutators reject null arguments",
                        _ =>
                        {
                            Check.Throws<ArgumentNullException>(() => new StreamingTranscript(null!), "null pane");
                            Pane pane = new Pane("t");
                            StreamingTranscript transcript = new StreamingTranscript(pane);
                            Check.Throws<ArgumentNullException>(() => transcript.AppendText(null!), "null append");
                            Check.Throws<ArgumentNullException>(() => transcript.Track(null!), "null track key");
                            Check.Throws<ArgumentNullException>(() => transcript.Update(null!, "x"), "null update key");
                            Check.Throws<ArgumentNullException>(() => transcript.Update("k", (string)null!), "null update content");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("StreamingTranscript", "UnknownKey", "Updating an untracked key throws",
                        _ =>
                        {
                            Pane pane = new Pane("t");
                            StreamingTranscript transcript = new StreamingTranscript(pane);
                            Check.Throws<KeyNotFoundException>(() => transcript.Update("missing", "x"), "unknown key");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}

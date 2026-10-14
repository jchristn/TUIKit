namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Testing;

    /// <summary>
    /// Coverage for 1.7 item V3: <see cref="MarkdownRenderer.StreamingPrefix"/> and its use by
    /// <see cref="StreamingTranscript"/>.
    /// </summary>
    public static class MarkdownStreamingPrefixSuite
    {
        private const string _Id = "MarkdownStreamingPrefix";

        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: _Id,
                displayName: "Markdown Streaming Prefix",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(_Id, "HalfRow", "A half-received row is held back and a complete row is not",
                        _ =>
                        {
                            Check.Equal("text", MarkdownRenderer.StreamingPrefix("text\n| a | b"), "half row held");
                            Check.Equal(string.Empty, MarkdownRenderer.StreamingPrefix("| a |"), "nothing safe yet");
                            Check.Equal("x\n|---|---|\n| 1 | 2 |", MarkdownRenderer.StreamingPrefix("x\n|---|---|\n| 1 | 2 |\n| 3"), "a body row waits for its newline");
                            string table = "| a | b |\n|---|---|\n| 1 | 2 |\n";
                            Check.True(ReferenceEquals(table, MarkdownRenderer.StreamingPrefix(table)), "complete rows returned as the same instance");
                            Check.Equal(string.Empty, MarkdownRenderer.StreamingPrefix(string.Empty), "empty input");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Header", "A header waits for its delimiter, stays held beside a partial row, and is released by a non-delimiter line",
                        _ =>
                        {
                            Check.Equal("x\n", MarkdownRenderer.StreamingPrefix("x\n\n| a | b |\n"), "header without a delimiter is held");
                            Check.Equal("x\n", MarkdownRenderer.StreamingPrefix("x\n\n| a | b |\n|--"), "a partial delimiter keeps both held");
                            Check.Equal("x\n\n| a | b |\n|---|---|\n", MarkdownRenderer.StreamingPrefix("x\n\n| a | b |\n|---|---|\n"), "released with the delimiter");
                            Check.Equal(string.Empty, MarkdownRenderer.StreamingPrefix("| a |\n| 1"), "a header followed by a partial row stays held");
                            Check.Equal(string.Empty, MarkdownRenderer.StreamingPrefix("| a |\n--"), "a partial line that could be a delimiter keeps the header held");
                            Check.Equal("| a |\nhello\n", MarkdownRenderer.StreamingPrefix("| a |\nhello\n"), "a complete non-delimiter line releases the header");
                            Check.Equal("| a |\nhel", MarkdownRenderer.StreamingPrefix("| a |\nhel"), "a partial line that cannot be a delimiter releases it");
                            Check.Equal("| a |\n| b |\n", MarkdownRenderer.StreamingPrefix("| a |\n| b |\n"), "a pipe line after another table line is not a header");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Unchanged", "Delimiter rows, prose pipes, tab or four-space indents, list items, and open emphasis stream as they arrive",
                        _ =>
                        {
                            string[] same = new[]
                            {
                                "|---|---|\n",
                                "a | b",
                                "text\n\t| a",
                                "text\n    | a",
                                "- partial ite",
                                "some **bol",
                                "plain\nlines\n",
                            };

                            foreach (string input in same)
                                Check.True(ReferenceEquals(input, MarkdownRenderer.StreamingPrefix(input)), "unchanged: " + input.Replace("\n", "\\n"));

                            Check.Equal("text", MarkdownRenderer.StreamingPrefix("text\n   | a"), "three spaces still count as a table row");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "NullThrows", "A null argument throws ArgumentNullException",
                        _ =>
                        {
                            Check.Throws<ArgumentNullException>(() => MarkdownRenderer.StreamingPrefix(null!), "null");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "SeededCharByChar", "Feeding seeded documents one character at a time never shows a short row or a header without its delimiter",
                        _ =>
                        {
                            Random random = new Random(20261014);
                            for (int n = 0; n < 150; n++)
                            {
                                string document = Document(random);
                                for (int length = 0; length <= document.Length; length++)
                                {
                                    string prefix = MarkdownRenderer.StreamingPrefix(document.Substring(0, length));
                                    Check.True(document.StartsWith(prefix, StringComparison.Ordinal), "a prefix of the input, doc " + n);
                                    AssertSafe(prefix, "doc " + n + " at " + length);
                                }
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "TranscriptHeader", "StreamingTranscript shows a header together with its delimiter row, and streams four-space code",
                        _ =>
                        {
                            Pane pane = new Pane("p");
                            StreamingTranscript transcript = new StreamingTranscript(pane);
                            transcript.RenderOptions = new MarkdownRenderOptions();
                            transcript.AppendText("Intro\n\n| Name | Size |\n");
                            string held = Snapshot.RenderWidget(pane, 40, 10);
                            Check.True(held.Contains("Intro"), "prose streams");
                            Check.False(held.Contains("Name"), "the header is not shown as text");
                            transcript.AppendText("|---|---|\n");
                            string table = Snapshot.RenderWidget(pane, 40, 10);
                            Check.True(table.Contains("│ Name"), "the header appears as a table");
                            transcript.FinalizeBlock();

                            Pane code = new Pane("c");
                            StreamingTranscript codeTranscript = new StreamingTranscript(code);
                            codeTranscript.RenderOptions = new MarkdownRenderOptions();
                            codeTranscript.AppendText("Example:\n\n    | not a table");
                            Check.True(Snapshot.RenderWidget(code, 40, 10).Contains("| not a table"), "an indented code line streams as it arrives");
                            return Task.CompletedTask;
                        }),
                });
        }

        private static string Document(Random random)
        {
            StringBuilder builder = new StringBuilder();
            int blocks = random.Next(1, 6);
            for (int b = 0; b < blocks; b++)
            {
                switch (random.Next(4))
                {
                    case 0:
                        builder.Append("Some prose with a | pipe and **bold** text.\n\n");
                        break;
                    case 1:
                        builder.Append("- item one\n- item two\n\n");
                        break;
                    case 2:
                        builder.Append("    | indented code\n\n");
                        break;
                    default:
                        int columns = random.Next(1, 5);
                        int rows = random.Next(0, 4);
                        builder.Append(Row(columns, "h")).Append('\n');
                        builder.Append('|');
                        for (int c = 0; c < columns; c++)
                            builder.Append(random.Next(2) == 0 ? "---|" : ":--:|");

                        builder.Append('\n');
                        for (int r = 0; r < rows; r++)
                            builder.Append(Row(columns, "v" + r)).Append('\n');

                        builder.Append('\n');
                        break;
                }
            }

            return builder.ToString();
        }

        private static string Row(int columns, string text)
        {
            StringBuilder builder = new StringBuilder("|");
            for (int c = 0; c < columns; c++)
                builder.Append(' ').Append(text).Append(c).Append(" |");

            return builder.ToString();
        }

        // Every table line in the prefix has its table's pipe count, and a header line is never the last table line
        // without its delimiter row.
        private static void AssertSafe(string prefix, string context)
        {
            string[] lines = prefix.Split('\n');
            int expectedPipes = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                bool tableLine = line.StartsWith("|", StringComparison.Ordinal);
                if (!tableLine)
                {
                    expectedPipes = -1;
                    continue;
                }

                int pipes = line.Split('|').Length - 1;
                if (expectedPipes < 0)
                {
                    expectedPipes = pipes;
                    bool hasDelimiter = i + 1 < lines.Length && lines[i + 1].StartsWith("|", StringComparison.Ordinal) && lines[i + 1].Contains("-");
                    Check.True(hasDelimiter, "a header is shown only with its delimiter row, " + context);
                }

                Check.Equal(expectedPipes, pipes, "every row has the header's cells, " + context);
            }
        }
    }
}

namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Content;

    /// <summary>
    /// Touchstone suite covering text wrapping and the <see cref="Pane"/>: partial-line streaming,
    /// mutable line handles, thread-safe concurrent writes, smart scroll lock, capacity eviction, and
    /// rendering.
    /// </summary>
    public static class ContentSuite
    {
        /// <summary>
        /// Builds the content suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Content",
                displayName: "Panes and Text Wrapping",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Content", "WordWrap", "Word wrapping breaks at spaces",
                        _ =>
                        {
                            IReadOnlyList<StyledText> rows = TextWrapper.Wrap(Text.From("hello world foo"), 11);
                            Check.Equal(2, rows.Count, "Row count");
                            Check.Equal("hello world", rows[0].ToPlainString(), "First row");
                            Check.Equal("foo", rows[1].ToPlainString(), "Second row");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Content", "HardWrap", "Overlong words hard break",
                        _ =>
                        {
                            IReadOnlyList<StyledText> rows = TextWrapper.Wrap(Text.From("abcdefghij"), 4);
                            Check.Equal(3, rows.Count, "Row count");
                            Check.Equal("abcd", rows[0].ToPlainString(), "First chunk");
                            Check.Equal("ij", rows[2].ToPlainString(), "Last chunk");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Content", "PartialLine", "Write accumulates then WriteLine commits",
                        _ =>
                        {
                            Pane pane = new Pane("p");
                            pane.Write("run");
                            pane.Write("ning");
                            Check.Equal(0, pane.LineCount, "No committed line yet");
                            pane.WriteLine("...");
                            Check.Equal(1, pane.LineCount, "One committed line");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Content", "MutableHandle", "A line can be updated in place",
                        _ =>
                        {
                            Pane pane = new Pane("p");
                            PaneLineHandle handle = pane.WriteLine("running...");
                            Check.True(handle.Update("done (1.2s)"), "Update succeeds");

                            CellBuffer buffer = new CellBuffer(20, 1);
                            pane.Render(new BufferSurface(buffer), CellStyle.Default);
                            Check.True(ReadRow(buffer, 0).StartsWith("done (1.2s)"), "Rendered updated content");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Content", "ConcurrentWrites", "Writes from many threads are all retained",
                        async ct =>
                        {
                            Pane pane = new Pane("p");
                            List<Task> tasks = new List<Task>();
                            for (int t = 0; t < 4; t++)
                            {
                                tasks.Add(Task.Run(() =>
                                {
                                    for (int i = 0; i < 25; i++)
                                        pane.WriteLine("line");
                                }, ct));
                            }

                            await Task.WhenAll(tasks).ConfigureAwait(false);
                            Check.Equal(100, pane.LineCount, "All lines retained");
                        }),

                    new TestCaseDescriptor("Content", "CapacityEviction", "Oldest lines evict past capacity",
                        _ =>
                        {
                            Pane pane = new Pane("p");
                            pane.ScrollbackCapacityLines = 3;
                            for (int i = 1; i <= 5; i++)
                                pane.WriteLine("L" + i);

                            Check.Equal(3, pane.LineCount, "Capacity enforced");
                            CellBuffer buffer = new CellBuffer(10, 3);
                            pane.Render(new BufferSurface(buffer), CellStyle.Default);
                            Check.True(ReadRow(buffer, 0).StartsWith("L3"), "Oldest retained is L3");
                            Check.True(ReadRow(buffer, 2).StartsWith("L5"), "Newest is L5");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Content", "ScrollLock", "Scrolling up detaches, bottom re-attaches",
                        _ =>
                        {
                            Pane pane = new Pane("p");
                            for (int i = 1; i <= 5; i++)
                                pane.WriteLine("L" + i);

                            CellBuffer buffer = new CellBuffer(10, 2);
                            BufferSurface surface = new BufferSurface(buffer);
                            pane.Render(surface, CellStyle.Default); // attached: shows L4, L5
                            Check.True(pane.IsAtBottom, "Starts attached");

                            pane.ScrollUp(1);
                            Check.False(pane.IsAtBottom, "Detached after scroll up");
                            pane.Render(surface, CellStyle.Default);
                            Check.True(ReadRow(buffer, 0).StartsWith("L3"), "Shows older content while detached");

                            pane.WriteLine("L6");
                            Check.Equal(1, pane.NewSinceDetached, "New line counted while detached");

                            pane.ScrollToBottom();
                            Check.True(pane.IsAtBottom, "Re-attached");
                            Check.Equal(0, pane.NewSinceDetached, "Indicator cleared");
                            pane.Render(surface, CellStyle.Default);
                            Check.True(ReadRow(buffer, 1).StartsWith("L6"), "Shows newest at bottom");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Content", "ByteGuard", "Overlong lines are truncated",
                        _ =>
                        {
                            Pane pane = new Pane("p");
                            pane.MaxLineLength = 10;
                            pane.WriteLine(new string('x', 5000));

                            CellBuffer buffer = new CellBuffer(20, 1);
                            pane.Render(new BufferSurface(buffer), CellStyle.Default);
                            string row = ReadRow(buffer, 0);
                            Check.True(row.Contains("…"), "Truncation ellipsis present");
                            Check.False(row.Contains(new string('x', 20)), "Not the full runaway line");
                            return Task.CompletedTask;
                        })
                });
        }

        private static string ReadRow(CellBuffer buffer, int row)
        {
            StringBuilder builder = new StringBuilder();
            for (int x = 0; x < buffer.Width; x++)
                builder.Append(buffer.Get(x, row).Grapheme);

            return builder.ToString().TrimEnd();
        }
    }
}

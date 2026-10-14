namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Theming;

    /// <summary>
    /// Coverage for 1.7 item V6: <see cref="MarkdownLayoutCache"/>.
    /// </summary>
    public static class MarkdownLayoutCacheSuite
    {
        private const string _Id = "MarkdownLayoutCache";

        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: _Id,
                displayName: "Markdown Layout Cache",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(_Id, "HitAndMiss", "A hit returns the same instance; a different text or options instance misses",
                        _ =>
                        {
                            MarkdownLayoutCache cache = new MarkdownLayoutCache();
                            MarkdownRenderOptions options = new MarkdownRenderOptions();
                            Check.Equal(256, cache.Capacity, "default capacity");
                            Check.Equal(0, cache.Width, "no width while empty");
                            IReadOnlyList<StyledText> first = cache.GetRows("# Hi", 40, options);
                            Check.True(ReferenceEquals(first, cache.GetRows("# Hi", 40, options)), "hit");
                            Check.Equal(1, cache.Count, "one entry");
                            Check.Equal(40, cache.Width, "cached width");
                            Check.False(ReferenceEquals(first, cache.GetRows("# Ho", 40, options)), "different text");
                            Check.False(ReferenceEquals(first, cache.GetRows("# Hi", 40, options.Clone())), "different options instance");
                            Check.Equal(3, cache.Count, "three entries");
                            Check.Equal(Plain(MarkdownRenderer.Render("# Hi", 40, options)), Plain(first), "same rows as a direct render");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "WidthDropsOthers", "A request at a new width drops every entry at other widths",
                        _ =>
                        {
                            MarkdownLayoutCache cache = new MarkdownLayoutCache();
                            MarkdownRenderOptions options = new MarkdownRenderOptions();
                            IReadOnlyList<StyledText> wide = cache.GetRows("a", 40, options);
                            cache.GetRows("b", 40, options);
                            cache.GetRows("c", 40, options);
                            cache.GetRows("a", 41, options);
                            Check.Equal(1, cache.Count, "only the new width remains");
                            Check.Equal(41, cache.Width, "width moved");
                            Check.False(ReferenceEquals(wide, cache.GetRows("a", 40, options)), "the old width was dropped");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "WithThemeMisses", "Two WithTheme results for the same theme miss each other",
                        _ =>
                        {
                            MarkdownLayoutCache cache = new MarkdownLayoutCache();
                            MarkdownRenderOptions options = new MarkdownRenderOptions();
                            IReadOnlyList<StyledText> a = cache.GetRows("x", 40, options.WithTheme(Theme.Dark));
                            IReadOnlyList<StyledText> b = cache.GetRows("x", 40, options.WithTheme(Theme.Dark));
                            Check.False(ReferenceEquals(a, b), "reference comparison of options");
                            MarkdownRenderOptions kept = options.WithTheme(Theme.Dark);
                            Check.True(ReferenceEquals(cache.GetRows("x", 40, kept), cache.GetRows("x", 40, kept)), "a kept instance hits");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "LeastRecentlyUsed", "Eviction follows least-recent use at capacity, and Clear empties the cache",
                        _ =>
                        {
                            MarkdownLayoutCache cache = new MarkdownLayoutCache { Capacity = 2 };
                            MarkdownRenderOptions options = new MarkdownRenderOptions();
                            IReadOnlyList<StyledText> a = cache.GetRows("a", 40, options);
                            IReadOnlyList<StyledText> b = cache.GetRows("b", 40, options);
                            cache.GetRows("a", 40, options);
                            cache.GetRows("c", 40, options);
                            Check.Equal(2, cache.Count, "at capacity");
                            Check.True(ReferenceEquals(a, cache.GetRows("a", 40, options)), "recently used entry kept");
                            Check.False(ReferenceEquals(b, cache.GetRows("b", 40, options)), "least recently used entry evicted");
                            cache.Capacity = 1;
                            Check.Equal(1, cache.Count, "lowering capacity evicts at once");
                            cache.Clear();
                            Check.Equal(0, cache.Count, "cleared");
                            Check.Equal(0, cache.Width, "no width after clear");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Guards", "Null arguments, a width below 1, and a capacity out of range throw",
                        _ =>
                        {
                            MarkdownLayoutCache cache = new MarkdownLayoutCache();
                            MarkdownRenderOptions options = new MarkdownRenderOptions();
                            Check.Throws<ArgumentNullException>(() => cache.GetRows(null!, 10, options), "null markdown");
                            Check.Throws<ArgumentNullException>(() => cache.GetRows("x", 10, null!), "null options");
                            Check.Throws<ArgumentOutOfRangeException>(() => cache.GetRows("x", 0, options), "width 0");
                            Check.Throws<ArgumentOutOfRangeException>(() => cache.Capacity = 0, "capacity 0");
                            Check.Throws<ArgumentOutOfRangeException>(() => cache.Capacity = 1000001, "capacity above the maximum");
                            cache.Capacity = 1000000;
                            Check.Equal(1000000, cache.Capacity, "maximum accepted");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Async", "GetRowsAsync returns cached rows and honors cancellation",
                        async _ =>
                        {
                            MarkdownLayoutCache cache = new MarkdownLayoutCache();
                            MarkdownRenderOptions options = new MarkdownRenderOptions();
                            IReadOnlyList<StyledText> rows = await cache.GetRowsAsync("x", 20, options, CancellationToken.None).ConfigureAwait(false);
                            Check.True(ReferenceEquals(rows, cache.GetRows("x", 20, options)), "shares entries with GetRows");
                            using (CancellationTokenSource source = new CancellationTokenSource())
                            {
                                source.Cancel();
                                await Check.ThrowsAsync<OperationCanceledException>(() => cache.GetRowsAsync("y", 20, options, source.Token), "cancelled").ConfigureAwait(false);
                            }

                            Check.Equal(1, cache.Count, "a cancelled call stores nothing");
                        }),

                    new TestCaseDescriptor(_Id, "Concurrent", "Concurrent GetRows from several threads returns consistent rows",
                        _ =>
                        {
                            MarkdownLayoutCache cache = new MarkdownLayoutCache { Capacity = 4 };
                            MarkdownRenderOptions options = new MarkdownRenderOptions();
                            string[] texts = new string[10];
                            string[] expected = new string[10];
                            for (int i = 0; i < texts.Length; i++)
                            {
                                texts[i] = "| k | v |\n|---|---|\n| row | " + i + " |\n\n- item " + i;
                                expected[i] = Plain(MarkdownRenderer.Render(texts[i], 40, options));
                            }

                            int failures = 0;
                            List<Thread> threads = new List<Thread>();
                            for (int t = 0; t < 8; t++)
                            {
                                int seed = t;
                                Thread thread = new Thread(() =>
                                {
                                    Random random = new Random(seed);
                                    for (int n = 0; n < 300; n++)
                                    {
                                        int index = random.Next(texts.Length);
                                        if (Plain(cache.GetRows(texts[index], 40, options)) != expected[index])
                                            Interlocked.Increment(ref failures);
                                    }
                                });
                                threads.Add(thread);
                                thread.Start();
                            }

                            foreach (Thread thread in threads)
                                thread.Join();

                            Check.Equal(0, failures, "every result matched a direct render");
                            Check.True(cache.Count <= 4, "capacity respected");
                            return Task.CompletedTask;
                        }),
                });
        }

        private static string Plain(IReadOnlyList<StyledText> rows)
        {
            List<string> lines = new List<string>();
            foreach (StyledText row in rows)
                lines.Add(row.ToPlainString());

            return string.Join("\n", lines);
        }
    }
}

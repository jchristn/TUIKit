namespace TUIKit.Example
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Modals;

    /// <summary>
    /// A fake agent that streams markdown tokens into the transcript, runs tool calls whose status
    /// lines mutate in place, and drives the telemetry stats. It has no network or model dependency,
    /// so the demo is self-contained and reproducible. Runs on a background thread to prove the
    /// thread-safe pane API.
    /// </summary>
    internal sealed class SimulatedAgent
    {
        private readonly Pane _Transcript;
        private readonly Pane _Tools;
        private readonly HarnessState _State;
        private readonly Random _Random = new Random(1234);
        private CancellationTokenSource? _Cts;
        private Task? _Loop;

        // Chunks are streamed one after another. A blank chunk is an intentional paragraph break;
        // there are no trailing newlines, so lines that belong together (the bullets, the code block)
        // stay together without stray blank lines between them.
        private static readonly string[] _Reply =
        {
            "# Analysis",
            "",
            "I looked at the **repository** and the `build` step is green. A few notes:",
            "- Coverage sits around *87%*.",
            "- One flaky test in the network suite.",
            "- Docs mention https://example.com/guide for setup.",
            "",
            "> Recommendation: pin the flaky test and move on.",
            "",
            "```\n$ dotnet test\nPassed! 214 tests\n```",
            "",
            "Let me run a couple of tools to confirm."
        };

        internal SimulatedAgent(Pane transcript, Pane tools, HarnessState state)
        {
            _Transcript = transcript ?? throw new ArgumentNullException(nameof(transcript));
            _Tools = tools ?? throw new ArgumentNullException(nameof(tools));
            _State = state ?? throw new ArgumentNullException(nameof(state));
        }

        internal void Start()
        {
            _Cts = new CancellationTokenSource();
            _Loop = Task.Run(() => RunAsync(_Cts.Token));
        }

        internal void Stop()
        {
            _Cts?.Cancel();
        }

        /// <summary>
        /// Produces a burst of content synchronously, used to seed the headless snapshot demo.
        /// </summary>
        internal void SeedOnce()
        {
            _Transcript.WriteLine(Text.From("user").Cyan().Bold().Append(Text.From("  summarize the build")));
            foreach (string chunk in _Reply)
                RenderChunk(chunk);

            PaneLineHandle handle = _Tools.WriteLine(Text.From("running").Yellow().Append(Text.From("  dotnet test")));
            handle.Update(Text.From("done").Green().Append(Text.From("  dotnet test (1.2s)")));
            _State.PushCpu(0.62);
            _State.PushCpu(0.71);
            _State.PushCpu(0.55);
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(300, cancellationToken).ConfigureAwait(false);
                _Transcript.WriteLine(Text.From("user").Cyan().Bold().Append(Text.From("  summarize the build")));

                while (!cancellationToken.IsCancellationRequested)
                {
                    for (int i = 0; i < _Reply.Length && !cancellationToken.IsCancellationRequested; i++)
                    {
                        RenderChunk(_Reply[i]);
                        _State.PushCpu(0.3 + (_Random.NextDouble() * 0.6));
                        await Task.Delay(120, cancellationToken).ConfigureAwait(false);
                    }

                    PaneLineHandle handle = _Tools.WriteLine(Text.From("running").Yellow().Append(Text.From("  dotnet test")));
                    _State.ActiveTool = true;
                    for (int step = 0; step <= 10 && !cancellationToken.IsCancellationRequested; step++)
                    {
                        _State.ToolProgress = step / 10.0;
                        await Task.Delay(90, cancellationToken).ConfigureAwait(false);
                    }

                    handle.Update(Text.From("done").Green().Append(Text.From("  dotnet test (0.9s)")));
                    _State.ActiveTool = false;
                    _State.Notifications.Add("Tool finished: dotnet test", NotificationSeverity.Success, _State.NowMilliseconds(), 3000);

                    await Task.Delay(1500, cancellationToken).ConfigureAwait(false);
                    _Transcript.WriteLine("");
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown.
            }
        }

        private void RenderChunk(string markdown)
        {
            System.Collections.Generic.IReadOnlyList<StyledText> lines = MarkdownRenderer.Render(markdown);
            using (_Transcript.BeginBatch())
            {
                for (int i = 0; i < lines.Count; i++)
                    _Transcript.WriteLine(lines[i]);
            }
        }
    }
}

namespace TUIKit.Example
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Hosting;
    using TUIKit.Terminal;
    using TUIKit.Theming;

    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            bool once = Array.IndexOf(args, "--once") >= 0;

            if (once)
            {
                RunHeadlessSnapshot(args);
                return 0;
            }

            if (Array.IndexOf(args, "--tour-once") >= 0)
            {
                RunTourSnapshot(args);
                return 0;
            }

            if (Array.IndexOf(args, "--contract-once") >= 0)
            {
                RunContractSnapshot();
                return 0;
            }

            if (Array.IndexOf(args, "--focus-once") >= 0)
            {
                RunFocusSnapshot();
                return 0;
            }

            if (Array.IndexOf(args, "--focus-audit-once") >= 0)
                return RunFocusAudit();

            if (Array.IndexOf(args, "--markdown-once") >= 0)
            {
                RunMarkdownSnapshot(args);
                return 0;
            }

            if (Array.IndexOf(args, "--diff-once") >= 0)
            {
                RunDiffSnapshot(args);
                return 0;
            }

            if (Array.IndexOf(args, "--calendar-once") >= 0)
            {
                RunCalendarSnapshot(args);
                return 0;
            }

            bool harnessMode = Array.IndexOf(args, "--harness") >= 0;
            bool contractMode = Array.IndexOf(args, "--contract") >= 0;
            bool focusMode = Array.IndexOf(args, "--focus") >= 0;

            using (ConsoleBackend backend = new ConsoleBackend())
            using (TuiApplication app = new TuiApplication(backend))
            {
                Console.CancelKeyPress += (sender, eventArgs) =>
                {
                    eventArgs.Cancel = true;
                    app.RequestStop();
                };

                if (focusMode)
                {
                    using (FocusShowcase showcase = new FocusShowcase(app))
                    {
                        showcase.StartLog();
                        await app.RunAsync(CancellationToken.None).ConfigureAwait(false);
                    }
                }
                else if (contractMode)
                {
                    ContractDemo demo = new ContractDemo(app);
                    demo.Start();
                    try
                    {
                        await app.RunAsync(CancellationToken.None).ConfigureAwait(false);
                    }
                    finally
                    {
                        demo.Stop();
                    }
                }
                else if (harnessMode)
                {
                    HarnessApp harness = new HarnessApp(app);
                    harness.StartLive();
                    try
                    {
                        await app.RunAsync(CancellationToken.None).ConfigureAwait(false);
                    }
                    finally
                    {
                        harness.StopLive();
                    }
                }
                else
                {
                    GuidedTour tour = new GuidedTour(app);
                    tour.Start();
                    try
                    {
                        await app.RunAsync(CancellationToken.None).ConfigureAwait(false);
                    }
                    finally
                    {
                        tour.Stop();
                    }
                }
            }

            return 0;
        }

        private static void RunHeadlessSnapshot(string[] args)
        {
            HeadlessBackend backend = new HeadlessBackend(100, 30);
            using (TuiApplication app = new TuiApplication(backend))
            {
                HarnessApp harness = new HarnessApp(app);
                app.Start();
                bool debug = Array.IndexOf(args, "--debug") >= 0;
                bool help = Array.IndexOf(args, "--help") >= 0;
                bool confirm = Array.IndexOf(args, "--confirm") >= 0;
                bool light = Array.IndexOf(args, "--light") >= 0;
                string frame = harness.RenderSeededFrame(100, 30, debug, help, confirm, light);
                app.Stop();

                Console.WriteLine("TUIKit example — headless snapshot (--once)");
                Console.WriteLine(new string('=', 100));
                Console.WriteLine(frame);
            }
        }

        private static void RunContractSnapshot()
        {
            HeadlessBackend backend = new HeadlessBackend(100, 30);
            using (TuiApplication app = new TuiApplication(backend))
            {
                ContractDemo demo = new ContractDemo(app);
                app.Start();
                app.RenderOnce();
                string frame = demo.RenderFrame(100, 30);
                app.Stop();

                Console.WriteLine("TUIKit example — interaction contract demo (--contract-once)");
                Console.WriteLine(new string('=', 100));
                Console.WriteLine(frame);
            }
        }

        private static void RunFocusSnapshot()
        {
            HeadlessBackend backend = new HeadlessBackend(100, 20);
            using (TuiApplication app = new TuiApplication(backend))
            using (FocusShowcase showcase = new FocusShowcase(app))
            {
                app.Start();
                app.RenderOnce();
                string frame = TUIKit.Testing.Snapshot.ToText(app.CaptureFrame()!);
                app.Stop();

                Console.WriteLine("TUIKit example: focus showcase (--focus-once)");
                Console.WriteLine(new string('=', 100));
                Console.WriteLine(frame);
            }
        }

        private static int RunFocusAudit()
        {
            // The audit app does not claim the terminal (1.5.0), so it can run beside another application
            // in the same process, the way a parallel UI test would.
            HeadlessBackend backend = new HeadlessBackend(100, 20) { ClaimsTerminal = false };
            using (TuiApplication app = new TuiApplication(backend))
            using (FocusShowcase showcase = new FocusShowcase(app))
            {
                app.Start();
                TUIKit.Testing.FocusAuditResult result = TUIKit.Testing.FocusAudit.Run(app);
                app.Stop();

                Console.WriteLine("TUIKit example: focus audit of the focus showcase (--focus-audit-once)");
                Console.WriteLine("Stops: " + result.Stops + "  Problems: " + result.Problems.Count);
                foreach (TUIKit.Testing.FocusAuditProblem problem in result.Problems)
                    Console.WriteLine("  " + problem);

                return result.IsClean ? 0 : 1;
            }
        }

        // Prints the Markdown tour sample at a given width with real colors, so the layout can be checked in any
        // terminal: --markdown-once [--width N] [--ascii] [--color] [--theme dark|light|contrast]. --color keeps
        // colors when output is piped. --theme lays the sample out with MarkdownRenderOptions.WithTheme over the
        // theme's background, which shows through every element because themed styles carry no backgrounds.
        private static void RunMarkdownSnapshot(string[] args)
        {
            int width = 80;
            int flag = Array.IndexOf(args, "--width");
            if (flag >= 0 && flag + 1 < args.Length)
                int.TryParse(args[flag + 1], out width);

            width = Math.Max(1, width);
            MarkdownRenderOptions options = Array.IndexOf(args, "--ascii") >= 0
                ? MarkdownRenderOptions.Ascii()
                : MarkdownDemoWidget.BuildOptions(MarkdownTableGlyphs.Rounded, 0, MarkdownOverflow.Wrap, MarkdownTableFallback.Stack, true);

            StyledConsole console = Array.IndexOf(args, "--color") >= 0
                ? new StyledConsole(Console.Out, TerminalColorDepth.TrueColor)
                : StyledConsole.ForStandardOutput();
            Theme? theme = null;
            int themeFlag = Array.IndexOf(args, "--theme");
            if (themeFlag >= 0 && themeFlag + 1 < args.Length)
            {
                string name = args[themeFlag + 1];
                theme = name == "light" ? Theme.Light : name == "contrast" ? Theme.HighContrast : Theme.Dark;
                options = options.WithTheme(theme);
            }

            console.WriteLine("TUIKit example: Markdown at width " + width + " (--markdown-once" + (theme == null ? string.Empty : " --theme " + theme.Name) + ")");
            console.WriteLine(new string('=', width));
            IReadOnlyList<StyledText> rows = MarkdownRenderer.Render(MarkdownDemoWidget.Sample, width, options);
            for (int i = 0; i < rows.Count; i++)
                console.WriteLine(theme == null ? rows[i] : OverBackground(rows[i], theme.Text, width));
        }

        // --diff-once [--width N] [--color] [--numbers]: prints the bundled patch through DiffView.FromUnifiedDiff.
        private static void RunDiffSnapshot(string[] args)
        {
            int width = 100;
            int flag = Array.IndexOf(args, "--width");
            if (flag >= 0 && flag + 1 < args.Length)
                int.TryParse(args[flag + 1], out width);

            width = Math.Max(10, width);
            TUIKit.Widgets.DiffView view = TUIKit.Widgets.DiffView.FromUnifiedDiff(SamplePatch.Text, "csharp");
            view.ShowLineNumbers = Array.IndexOf(args, "--numbers") >= 0;
            CellBuffer buffer = new CellBuffer(width, Math.Max(1, view.LineCount));
            view.Render(new BufferSurface(buffer));
            StyledConsole console = Array.IndexOf(args, "--color") >= 0
                ? new StyledConsole(Console.Out, TerminalColorDepth.TrueColor)
                : StyledConsole.ForStandardOutput();
            console.WriteLine("TUIKit example: the bundled patch at width " + width + " (--diff-once), files: " + string.Join(", ", view.Files));
            console.WriteLine(new string('=', width));
            WriteBuffer(console, buffer);
        }

        // --calendar-once [--ascii] [--width N] [--color]: prints a year of generated activity through CalendarHeatMap.
        private static void RunCalendarSnapshot(string[] args)
        {
            int width = 120;
            int flag = Array.IndexOf(args, "--width");
            if (flag >= 0 && flag + 1 < args.Length)
                int.TryParse(args[flag + 1], out width);

            width = Math.Max(20, width);
            CalendarDemoWidget demo = new CalendarDemoWidget(null);
            TUIKit.Widgets.CalendarHeatMap calendar = demo.Calendar;
            if (Array.IndexOf(args, "--ascii") >= 0)
                calendar.LevelGlyphs = new[] { " ", ".", ":", "+", "#" };
            calendar.CellWidth = width >= 110 ? 2 : 1;
            CellBuffer buffer = new CellBuffer(width, 10);
            calendar.Render(new BufferSurface(buffer));
            StyledConsole console = Array.IndexOf(args, "--color") >= 0
                ? new StyledConsole(Console.Out, TerminalColorDepth.TrueColor)
                : StyledConsole.ForStandardOutput();
            console.WriteLine("TUIKit example: a year of activity at width " + width + " (--calendar-once" + (calendar.LevelGlyphs != null ? " --ascii" : string.Empty) + ")");
            console.WriteLine(new string('=', width));
            WriteBuffer(console, buffer);
        }

        // Writes a rendered buffer row by row as styled text, trimming trailing blanks.
        private static void WriteBuffer(StyledConsole console, CellBuffer buffer)
        {
            for (int y = 0; y < buffer.Height; y++)
            {
                List<StyledSpan> spans = new List<StyledSpan>();
                int last = buffer.Width - 1;
                while (last >= 0 && string.IsNullOrWhiteSpace(buffer.Get(last, y).Grapheme))
                    last--;

                for (int x = 0; x <= last; x++)
                {
                    Cell cell = buffer.Get(x, y);
                    if (!cell.IsContinuation)
                        spans.Add(new StyledSpan(string.IsNullOrEmpty(cell.Grapheme) ? " " : cell.Grapheme, cell.Style));
                }

                console.WriteLine(new StyledText(spans));
            }
        }

        // Paints a row over a backdrop style and pads it to the width, so a themed snapshot shows its background.
        private static StyledText OverBackground(StyledText row, CellStyle backdrop, int width)
        {
            List<StyledSpan> spans = new List<StyledSpan>();
            foreach (StyledSpan span in row.Spans)
                spans.Add(new StyledSpan(span.Text, span.Style.Over(backdrop)));

            int pad = Math.Max(0, width - row.Width);
            if (pad > 0)
                spans.Add(new StyledSpan(new string(' ', pad), backdrop));

            return new StyledText(spans);
        }

        // Splits typed input into single keys: a CSI escape sequence (ESC [ ... final) is one key, as is each other
        // character.
        private static List<string> SplitKeys(string keys)
        {
            List<string> result = new List<string>();
            int i = 0;
            while (i < keys.Length)
            {
                int end = i + 1;
                if (keys[i] == '\u001b' && end < keys.Length && keys[end] == '[')
                {
                    end++;
                    while (end < keys.Length && !(char.IsLetter(keys[end]) || keys[end] == '~'))
                        end++;

                    end = Math.Min(keys.Length, end + 1);
                }

                result.Add(keys.Substring(i, end - i));
                i = end;
            }

            return result;
        }

        private static void RunTourSnapshot(string[] args)
        {
            int page = 0;
            int flag = Array.IndexOf(args, "--page");
            if (flag >= 0 && flag + 1 < args.Length)
                int.TryParse(args[flag + 1], out page);

            bool help = Array.IndexOf(args, "--help") >= 0;

            bool nav = Array.IndexOf(args, "--nav") >= 0;

            HeadlessBackend backend = new HeadlessBackend(100, 30);
            using (TuiApplication app = new TuiApplication(backend))
            {
                GuidedTour tour = new GuidedTour(app);
                app.Start();
                if (nav)
                {
                    backend.FeedInput("\u001b[6~"); // PageDown
                    app.PumpInputOnce();
                    backend.FeedInput("\u001b[6~"); // PageDown
                    app.PumpInputOnce();
                    backend.FeedInput("\u001b[5~"); // PageUp
                    app.PumpInputOnce();
                }

                int keysFlag = Array.IndexOf(args, "--keys");
                if (keysFlag >= 0 && keysFlag + 1 < args.Length)
                {
                    // Feed one key at a time and render a frame after each, as a terminal would, so widgets know
                    // their size before the next key arrives.
                    foreach (string key in SplitKeys(args[keysFlag + 1]))
                    {
                        backend.FeedInput(key);
                        app.PumpInputOnce();
                        tour.RenderFrame(100, 30, -1, help);
                    }

                    page = -1; // keep the state produced by the keys
                }

                string frame = tour.RenderFrame(100, 30, page, help);
                app.Stop();

                Console.WriteLine("TUIKit guided tour — headless snapshot (--tour-once --page " + page + ")");
                Console.WriteLine(new string('=', 100));
                Console.WriteLine(frame);
            }
        }
    }
}

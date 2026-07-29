namespace TUIKit.Example
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using TUIKit;
    using TUIKit.Hosting;
    using TUIKit.Terminal;

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

            bool harnessMode = Array.IndexOf(args, "--harness") >= 0;

            using (ConsoleBackend backend = new ConsoleBackend())
            using (TuiApplication app = new TuiApplication(backend))
            {
                Console.CancelKeyPress += (sender, eventArgs) =>
                {
                    eventArgs.Cancel = true;
                    app.RequestStop();
                };

                if (harnessMode)
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

        private static void RunTourSnapshot(string[] args)
        {
            int page = 0;
            int flag = Array.IndexOf(args, "--page");
            if (flag >= 0 && flag + 1 < args.Length)
                int.TryParse(args[flag + 1], out page);

            bool help = Array.IndexOf(args, "--help") >= 0;

            HeadlessBackend backend = new HeadlessBackend(100, 30);
            using (TuiApplication app = new TuiApplication(backend))
            {
                GuidedTour tour = new GuidedTour(app);
                app.Start();
                string frame = tour.RenderFrame(100, 30, page, help);
                app.Stop();

                Console.WriteLine("TUIKit guided tour — headless snapshot (--tour-once --page " + page + ")");
                Console.WriteLine(new string('=', 100));
                Console.WriteLine(frame);
            }
        }
    }
}

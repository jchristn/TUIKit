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

            using (ConsoleBackend backend = new ConsoleBackend())
            using (TuiApplication app = new TuiApplication(backend))
            {
                HarnessApp harness = new HarnessApp(app);

                Console.CancelKeyPress += (sender, eventArgs) =>
                {
                    eventArgs.Cancel = true;
                    app.RequestStop();
                };

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
                string frame = harness.RenderSeededFrame(100, 30, debug);
                app.Stop();

                Console.WriteLine("TUIKit example — headless snapshot (--once)");
                Console.WriteLine(new string('=', 100));
                Console.WriteLine(frame);
            }
        }
    }
}

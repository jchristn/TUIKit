namespace TuiKitApp
{
    using System.Threading;
    using System.Threading.Tasks;
    using TUIKit.Content;
    using TUIKit.Hosting;

    /// <summary>
    /// A minimal TUIKit terminal application. It lays out a one-row header and a filling body, writes
    /// some styled content, and quits on Ctrl+Q. Extend it by binding more chords and widgets.
    /// </summary>
    internal static class Program
    {
        private static async Task Main()
        {
            await TuiApp.RunAsync(app =>
            {
                Pane header = app.AddPane("header", region => region.TopAnchored(0, 1).FillWidth());
                header.WriteMarkup("[bold]TuiKitApp[/]  —  press [yellow]Ctrl+Q[/] to quit");

                Pane body = app.AddPane("body", region => region.FillHeight(1, 0).FillWidth());
                body.WriteLine("Welcome to your new TUIKit app!");
                body.WriteLine(string.Empty);
                body.WriteMarkup("Edit [green]Program.cs[/] to build your interface.");

                app.Bind("Ctrl+Q", () => app.Quit());
            },
            CancellationToken.None).ConfigureAwait(false);
        }
    }
}

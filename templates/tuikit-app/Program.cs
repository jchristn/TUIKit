namespace TuiKitApp
{
    using System.Threading;
    using System.Threading.Tasks;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Hosting;
    using TUIKit.Widgets;

    /// <summary>
    /// A minimal TUIKit terminal application: a header, a list and a notes field in framed regions, and
    /// a footer. The focused region draws a heavy frame, Tab moves focus, the footer lists the keys that
    /// work for whatever has focus, and Ctrl+Q quits. Extend it by binding more chords and widgets.
    /// </summary>
    internal static class Program
    {
        private static async Task Main()
        {
            await TuiApp.RunAsync(app =>
            {
                // Bordered regions show which one has focus, so keys never go somewhere unexpected.
                app.HighlightFocusedRegion = true;

                Pane header = app.AddPane("header", region => region.TopAnchored(0, 1).FillWidth().WithPadding(0));
                header.WriteMarkup("[bold]TuiKitApp[/]  -  [yellow]Tab[/] moves focus, [yellow]Ctrl+Q[/] quits");

                ListView<string> items = app.AddWidget("items", new ListView<string>(), region => region
                    .ProportionalWidth(0.0, 0.4).FillHeight(1, 1).WithPadding(0).WithBorder(BorderStyle.Rounded, "Items"));
                items.SetItems(new[] { "First item", "Second item", "Third item" });

                app.AddWidget("notes", new TextField { Placeholder = "Type a note" }, region => region
                    .ProportionalWidth(0.4, 0.6).FillHeight(1, 1).WithPadding(0).WithBorder(BorderStyle.Rounded, "Notes"));

                // The footer follows focus: it lists the focused widget's keys, then the app's.
                StatusBar footer = app.AddWidget("footer", new StatusBar(), region => region.BottomAnchored(0, 1).FillWidth().WithPadding(0));
                app.BindKeyHints(footer).AddAppHint("ctrl+q", "Quit");

                app.Bind("Ctrl+Q", () => app.Quit());
            },
            CancellationToken.None).ConfigureAwait(false);
        }
    }
}

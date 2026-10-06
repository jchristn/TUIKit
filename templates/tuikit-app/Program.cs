namespace TuiKitApp
{
    using System.Threading;
    using System.Threading.Tasks;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Hosting;
    using TUIKit.Layout;
    using TUIKit.Widgets;

    /// <summary>
    /// A minimal TUIKit terminal application: a header, a two-pane framed screen (a list beside a notes
    /// field), and a footer. The panes share one border line, the focused pane is drawn whole in the
    /// focus style, Tab moves focus, the footer lists the keys that work for whatever has focus, and
    /// Ctrl+Q quits. Extend it by adding panes to the stack and binding more chords.
    /// </summary>
    internal static class Program
    {
        private static async Task Main()
        {
            await TuiApp.RunAsync(app =>
            {
                Pane header = app.AddPane("header", region => region.TopAnchored(0, 1).FillWidth().WithPadding(0));
                header.WriteMarkup("[bold]TuiKitApp[/]  -  [yellow]Tab[/] moves focus, [yellow]Ctrl+Q[/] quits");

                ListView<string> items = new ListView<string>();
                items.SetItems(new[] { "First item", "Second item", "Third item" });
                TextField notes = new TextField { Placeholder = "Type a note" };

                // One framed screen: the panes share a line, and the focused pane's frame is drawn whole.
                FramedStack screen = new FramedStack(SplitOrientation.Horizontal)
                    .Add(items, StackSize.Weighted(2), "Items")
                    .Add(notes, StackSize.Weighted(3), "Notes");
                screen.FrameOptions.TitleAlignment = TitleAlignment.Left;
                app.AddWidget("screen", screen, region => region.FillWidth().FillHeight(1, 1).WithPadding(0));

                // The footer follows focus: it lists the focused widget's keys, then the app's.
                StatusBar footer = app.AddWidget("footer", new StatusBar { RightText = "TuiKitApp" }, region => region.BottomAnchored(0, 1).FillWidth().WithPadding(0));
                app.BindKeyHints(footer).AddAppHint("ctrl+q", "Quit");

                app.Bind("Ctrl+Q", () => app.Quit());
            },
            CancellationToken.None).ConfigureAwait(false);
        }
    }
}

namespace TUIKit.Example
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Hosting;
    using TUIKit.Input;
    using TUIKit.Layout;
    using TUIKit.Widgets;

    /// <summary>
    /// A self-describing guided tour of TUIKit. A header names the feature being shown, the left
    /// "Live demo" pane renders the widget itself, and the right "Code" pane shows how to build it.
    /// PageUp/PageDown (or '[' / ']') browse the features; the arrow keys and Enter interact with the
    /// live widget; Ctrl+Q quits. This is the default experience when you run <c>TUIKit.Example</c>.
    /// </summary>
    internal sealed class GuidedTour
    {
        private readonly TuiApplication _App;
        private readonly List<TourPage> _Pages;
        private readonly Layout _Layout;
        private int _Index;

        internal GuidedTour(TuiApplication app)
        {
            _App = app ?? throw new ArgumentNullException(nameof(app));
            _Pages = BuildPages();

            _Layout = Layout.Create()
                .Add("header", r => r.FillWidth().TopAnchored(0, 1))
                .Add("desc", r => r.FillWidth().TopAnchored(1, 1).WithPadding(0))
                .Add("demo", r => r.ProportionalWidth(0.0, 0.5).Vertical(AxisConstraint.Stretch(2, 1)).WithBorder(BorderStyle.Rounded, "Live demo"))
                .Add("code", r => r.ProportionalWidth(0.5, 0.5).Vertical(AxisConstraint.Stretch(2, 1)).WithBorder(BorderStyle.Rounded, "Code"))
                .Add("footer", r => r.FillWidth().BottomAnchored(0, 1))
                .Build();

            _App.Layout = _Layout;
            _App.Commands.Register(KeyChord.Parse("ctrl+q"), "quit");
            _App.RegisterCommand("quit", () => _App.RequestStop());
            _App.CtrlCPolicy = CtrlCPolicy.DoubleTapToExit;
            _App.KeyReceived += OnKey;
            _App.RenderOverlay = Draw;
        }

        internal void Start()
        {
            _App.Start();
        }

        internal void Stop()
        {
            _App.Stop();
        }

        /// <summary>
        /// Renders a single tour frame to text (borders plus overlay) for headless smoke-testing and
        /// documentation screenshots.
        /// </summary>
        /// <param name="width">The frame width.</param>
        /// <param name="height">The frame height.</param>
        /// <param name="pageIndex">The zero-based page to show.</param>
        /// <returns>The frame as text.</returns>
        internal string RenderFrame(int width, int height, int pageIndex = 0)
        {
            _Index = ((pageIndex % _Pages.Count) + _Pages.Count) % _Pages.Count;

            CellBuffer buffer = new CellBuffer(width, height);
            BufferSurface surface = new BufferSurface(buffer);
            surface.Fill(new Rect(0, 0, width, height), Cell.Blank(_App.Theme.Text));

            foreach (Region region in _Layout.Regions)
            {
                if (!region.HasBorder)
                    continue;

                BorderStyle style = _App.Theme.UseAsciiBorders ? BorderStyle.Ascii : region.Border;
                surface.DrawBox(region.Resolve(buffer.Size), _App.Theme.Border, style, region.BorderTitle);
            }

            Draw(surface);
            return TUIKit.Testing.Snapshot.ToText(buffer);
        }

        private void OnKey(KeyEvent key)
        {
            if (key.Code == KeyCode.PageDown || IsChar(key, ']'))
            {
                _Index = (_Index + 1) % _Pages.Count;
                return;
            }

            if (key.Code == KeyCode.PageUp || IsChar(key, '['))
            {
                _Index = (_Index - 1 + _Pages.Count) % _Pages.Count;
                return;
            }

            if (_Pages[_Index].Demo is IFocusable focusable)
                focusable.HandleKey(key);
        }

        private static bool IsChar(KeyEvent key, char c)
        {
            return key.Code == KeyCode.Character && key.Rune == c;
        }

        private void Draw(ISurface root)
        {
            Size size = root.Size;
            if (!_Layout.FitsIn(size))
                return;

            BufferSurface? buffer = root as BufferSurface;
            if (buffer == null)
                return;

            TourPage page = _Pages[_Index];

            CellStyle bar = CellStyle.Default.WithForeground(Color.FromRgb(0, 0, 0)).WithBackground(Color.FromPalette(6));
            root.Fill(new Rect(0, 0, size.Width, 1), Cell.Blank(bar));
            string title = " " + Icons.Star + " " + page.Title + "   (" + (_Index + 1) + "/" + _Pages.Count + ")";
            root.DrawText(0, 0, title, bar.WithAttribute(CellAttributes.Bold, true));

            Rect descRect = _Layout.FindById("desc")!.ContentRect(size);
            if (!descRect.IsEmpty)
            {
                BufferSurface descView = buffer.CreateView(descRect);
                descView.DrawStyledText(0, 0, Markup.Parse(page.Description));
            }

            Rect demoRect = _Layout.FindById("demo")!.ContentRect(size);
            if (!demoRect.IsEmpty)
                page.Demo.Render(buffer.CreateView(demoRect));

            Rect codeRect = _Layout.FindById("code")!.ContentRect(size);
            if (!codeRect.IsEmpty)
            {
                BufferSurface codeView = buffer.CreateView(codeRect);
                for (int i = 0; i < page.Code.Count && i < codeRect.Height; i++)
                    codeView.DrawStyledText(0, i, SyntaxHighlighter.HighlightLine(page.Code[i], "csharp"));
            }

            CellStyle muted = _App.Theme.Muted;
            root.Fill(new Rect(0, size.Height - 1, size.Width, 1), Cell.Blank(muted));
            root.DrawText(0, size.Height - 1, " PgUp/PgDn or [ ]  browse    arrows/Enter  interact    Ctrl+Q  quit", muted);
        }

        private static List<TourPage> BuildPages()
        {
            List<TourPage> pages = new List<TourPage>();

            Pane markup = new Pane("markup");
            markup.WriteMarkup("[bold]Bold[/], [red]red[/], [green]green[/], [blue on white] on white [/]");
            markup.WriteLine(string.Empty);
            markup.WriteMarkup("Nest styles: [yellow]warn [bold]!!![/] still yellow[/]");
            pages.Add(new TourPage(
                "Markup & styled text",
                "[bold]Markup.Parse[/] and [bold]Pane.WriteMarkup[/] turn tags into styled spans.",
                markup,
                new[]
                {
                    "pane.WriteMarkup(",
                    "  \"[bold]Bold[/], [red]red[/], \" +",
                    "  \"[blue on white] on white [/]\");",
                    "",
                    "// Nested tags compose:",
                    "// [yellow]warn [bold]!!![/][/]"
                }));

            pages.Add(new TourPage(
                "Banner text (FIGlet)",
                "[bold]BannerText[/] renders big block letters from a built-in 5x5 font.",
                new BannerText("TUI"),
                new[]
                {
                    "BannerText banner =",
                    "  new BannerText(\"TUI\");",
                    "banner.Color = Color.FromPalette(6);",
                    "// Measures 5 cells tall."
                }));

            double[] wave = new double[48];
            for (int i = 0; i < wave.Length; i++)
                wave[i] = Math.Sin(i * 0.3) + Math.Sin(i * 0.11);
            pages.Add(new TourPage(
                "Line chart (braille)",
                "[bold]LineChart[/] plots a series onto a [bold]BrailleCanvas[/] at 2x4 resolution.",
                new LineChart(wave),
                new[]
                {
                    "double[] data = Sample();",
                    "LineChart chart =",
                    "  new LineChart(data);",
                    "chart.Render(surface);"
                }));

            pages.Add(new TourPage(
                "Bar chart",
                "[bold]BarChart[/] draws proportional bars with sub-cell block glyphs.",
                new BarChart().Add("cpu", 82).Add("mem", 47).Add("disk", 61).Add("net", 23),
                new[]
                {
                    "new BarChart()",
                    "  .Add(\"cpu\", 82)",
                    "  .Add(\"mem\", 47)",
                    "  .Add(\"disk\", 61);"
                }));

            MultiProgress progress = new MultiProgress();
            progress.Add("download", 0.72);
            progress.Add("extract", 0.40);
            progress.Add("verify", 1.0);
            pages.Add(new TourPage(
                "Multi-task progress",
                "[bold]MultiProgress[/] shows several [bold]ProgressTask[/] bars at once.",
                progress,
                new[]
                {
                    "MultiProgress p = new MultiProgress();",
                    "var t = p.Add(\"download\");",
                    "t.Report(0.72); // 0..1",
                    "// completed tasks turn green"
                }));

            DataTable<string> table = new DataTable<string>()
                .Column("Name", s => s.Split('|')[0])
                .Column("Role", s => s.Split('|')[1])
                .Column("Score", s => s.Split('|')[2], true);
            table.Bind(new List<string> { "Ada|admin|97", "Grace|dev|88", "Alan|ops|91" });
            pages.Add(new TourPage(
                "Data table",
                "[bold]DataTable<T>[/] is sortable and virtualized. Try [bold]Up/Down[/].",
                table,
                new[]
                {
                    "new DataTable<Person>()",
                    "  .Column(\"Name\", p => p.Name)",
                    "  .Column(\"Score\", p =>",
                    "     p.Score, sortable: true);",
                    "table.Bind(people);"
                }));

            Dictionary<string, string[]> tree = new Dictionary<string, string[]>
            {
                { "src", new[] { "TUIKit", "Tests" } },
                { "TUIKit", new[] { "Widgets", "Layout" } }
            };
            Tree<string> treeWidget = new Tree<string>(
                "src",
                s => tree.TryGetValue(s, out string[]? kids) ? kids : Array.Empty<string>(),
                s => s);
            pages.Add(new TourPage(
                "Tree",
                "[bold]Tree<T>[/] expands hierarchies lazily. [bold]Up/Down[/], [bold]Enter[/] toggles.",
                treeWidget,
                new[]
                {
                    "new Tree<string>(",
                    "  root: \"src\",",
                    "  children: s => ChildrenOf(s),",
                    "  label: s => s);"
                }));

            TabView tabs = new TabView()
                .Add("Overview", new Label(Text.From("Tab one content")))
                .Add("Details", new Label(Text.From("Tab two content")))
                .Add("Logs", new Label(Text.From("Tab three content")));
            pages.Add(new TourPage(
                "Tabs",
                "[bold]TabView[/] switches content panes. Press [bold]Tab[/] to cycle.",
                tabs,
                new[]
                {
                    "new TabView()",
                    "  .Add(\"Overview\", overview)",
                    "  .Add(\"Details\", details);",
                    "// Tab key cycles active tab"
                }));

            pages.Add(new TourPage(
                "Fuzzy finder",
                "[bold]FuzzyList[/] filters as you type. Type letters; [bold]Backspace[/] edits.",
                new FuzzyList(new[] { "apple", "apricot", "banana", "grape", "grapefruit", "mango" }),
                new[]
                {
                    "var list = new FuzzyList(items);",
                    "// typing filters:",
                    "list.Query = \"ap\";",
                    "var pick = list.SelectedItem;"
                }));

            MenuBar menu = new MenuBar();
            menu.AddMenu("File").Add("New").Add("Open").Add("Quit");
            menu.AddMenu("Edit").Add("Undo").Add("Redo");
            menu.AddMenu("View").Add("Zoom In").Add("Zoom Out");
            pages.Add(new TourPage(
                "Menu bar",
                "[bold]MenuBar[/]: [bold]Left/Right[/] pick a menu, [bold]Down[/] opens, [bold]Enter[/] runs.",
                menu,
                new[]
                {
                    "var bar = new MenuBar();",
                    "bar.AddMenu(\"File\")",
                    "   .Add(\"Open\", OpenFile)",
                    "   .Add(\"Quit\", app.Quit);"
                }));

            SplitView split = new SplitView(
                SplitOrientation.Vertical,
                new SplitView(SplitOrientation.Horizontal, new Label(Text.From("left")), new Label(Text.From("right"))),
                new Label(Text.From("bottom")));
            pages.Add(new TourPage(
                "Split view (nested)",
                "[bold]SplitView[/] nests and resizes. [bold]Up/Down[/] drags this divider.",
                split,
                new[]
                {
                    "new SplitView(Vertical,",
                    "  new SplitView(Horizontal,",
                    "    left, right),",
                    "  bottom);",
                    "// arrows drag the divider"
                }));

            pages.Add(new TourPage(
                "Color picker",
                "[bold]ColorPicker[/]: [bold]Up/Down[/] pick a channel, [bold]Left/Right[/] adjust it.",
                new ColorPicker(Color.FromRgb(64, 160, 220)),
                new[]
                {
                    "var picker = new ColorPicker(",
                    "  Color.FromRgb(64, 160, 220));",
                    "// picker.Value is the chosen Color",
                    "// PgUp/PgDn jump by 16"
                }));

            pages.Add(new TourPage(
                "Diff view",
                "[bold]DiffView[/] renders an LCS line diff: [green]added[/], [red]removed[/], context.",
                new DiffView("line one\nshared\nold middle\nlast", "line one\nshared\nnew middle\nlast", "csharp"),
                new[]
                {
                    "new DiffView(oldText, newText,",
                    "  language: \"csharp\");",
                    "// green +added, red -removed"
                }));

            KeyBindingSet bindings = new KeyBindingSet()
                .Add("save", "ctrl+s")
                .Add("quit", "ctrl+q")
                .Add("find", "ctrl+f")
                .Add("palette", "ctrl+p");
            pages.Add(new TourPage(
                "Key binding editor",
                "[bold]KeyBindingEditor[/]: [bold]Up/Down[/], [bold]Enter[/] then press a key to rebind.",
                new KeyBindingEditor(bindings),
                new[]
                {
                    "var set = new KeyBindingSet()",
                    "  .Add(\"save\", \"ctrl+s\")",
                    "  .Add(\"quit\", \"ctrl+q\");",
                    "var editor =",
                    "  new KeyBindingEditor(set);"
                }));

            pages.Add(new TourPage(
                "Image (half-block)",
                "[bold]HalfBlockImage[/] packs two pixels per cell with the [bold]▀[/] glyph.",
                new HalfBlockImage(40, 32, (x, y) => Color.FromRgb((byte)(x * 6), (byte)(y * 7), 160)),
                new[]
                {
                    "new HalfBlockImage(w, h,",
                    "  (x, y) => Color.FromRgb(",
                    "     r, g, b));",
                    "// 2x vertical resolution"
                }));

            return pages;
        }
    }
}

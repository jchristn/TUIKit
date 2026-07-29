namespace TUIKit.Example
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Hosting;
    using TUIKit.Input;
    using TUIKit.Layout;
    using TUIKit.Modals;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    /// <summary>
    /// A self-describing guided tour of TUIKit. A header names the feature being shown, the left
    /// "Live demo" pane renders the widget itself, and the right "Code" pane shows how to build it.
    /// Tab cycles focus between the "Live demo" and a full-width "Interactive" box that echoes typed
    /// text. PageUp/PageDown (or '[' / ']') browse the features while the Live demo is focused; the
    /// arrow keys and Enter interact with whichever box is focused. Global keys open live UI: F1 for
    /// help, Ctrl+G for the settings menu, Ctrl+T
    /// to cycle the theme, Ctrl+K for a confirmation dialog, Ctrl+N for a notification, Ctrl+Q to quit.
    /// This is the default experience when you run <c>TUIKit.Example</c>.
    /// </summary>
    internal sealed class GuidedTour
    {
        private readonly TuiApplication _App;
        private readonly List<TourPage> _Pages;
        private readonly Layout _Layout;
        private readonly List<string> _Events = new List<string>();
        private readonly object _EventsLock = new object();
        private const int FocusLiveDemo = 0;
        private const int FocusInteractive = 1;
        private const int FocusCount = 2;
        private int _Index;
        private int _ThemeIndex;
        private int _FocusIndex = FocusLiveDemo;
        private bool _ShowHelp;
        private string _Input = string.Empty;

        internal GuidedTour(TuiApplication app)
        {
            _App = app ?? throw new ArgumentNullException(nameof(app));
            _Pages = BuildPages();

            _Layout = Layout.Create()
                .Add("header", r => r.FillWidth().TopAnchored(0, 1))
                .Add("desc", r => r.FillWidth().TopAnchored(1, 1).WithPadding(0))
                .Add("demo", r => r.ProportionalWidth(0.0, 0.5).Vertical(AxisConstraint.Stretch(2, 13)).WithBorder(BorderStyle.Rounded, "Live demo"))
                .Add("code", r => r.ProportionalWidth(0.5, 0.5).Vertical(AxisConstraint.Stretch(2, 13)).WithBorder(BorderStyle.Rounded, "Code"))
                .Add("interactive", r => r.FillWidth().BottomAnchored(8, 5).WithBorder(BorderStyle.Rounded, "Interactive").WithPadding(1, 0, 1, 0))
                .Add("actions", r => r.FillWidth().BottomAnchored(1, 7).WithBorder(BorderStyle.Rounded, "Actions"))
                .Add("footer", r => r.FillWidth().BottomAnchored(0, 1))
                .Build();

            _App.Layout = _Layout;
            _App.Commands.Register(KeyChord.Parse("ctrl+q"), "quit");
            _App.Commands.Register(KeyChord.Parse("f1"), "help");
            _App.Commands.Register(KeyChord.Parse("ctrl+g"), "settings");
            _App.Commands.Register(KeyChord.Parse("ctrl+t"), "theme");
            _App.Commands.Register(KeyChord.Parse("ctrl+k"), "confirm");
            _App.Commands.Register(KeyChord.Parse("ctrl+n"), "notify");
            _App.Commands.Register(KeyChord.Parse("f12"), "mouse");

            _App.RegisterCommand("quit", () => _App.RequestStop());
            _App.RegisterCommand("help", () => { _ShowHelp = !_ShowHelp; Log(_ShowHelp ? "F1 was pressed, opening help" : "Help closed"); });
            _App.RegisterCommand("settings", OpenSettings);
            _App.RegisterCommand("theme", CycleTheme);
            _App.RegisterCommand("confirm", ConfirmDemo);
            _App.RegisterCommand("notify", () => { _App.Notify("This is a TUIKit notification toast.", NotificationSeverity.Info, 2500); Log("Ctrl+N was pressed, showing a notification toast"); });
            _App.RegisterCommand("mouse", ToggleMouse);

            _App.CtrlCPolicy = CtrlCPolicy.DoubleTapToExit;
            _App.KeyReceived += OnKey;
            _App.RenderOverlay = Draw;
        }

        private void ToggleMouse()
        {
            bool on = _App.ToggleMouseCapture();
            Log("F12 was pressed, mouse capture " + (on ? "ON" : "OFF"));
            _App.Notify(
                on
                    ? "Mouse capture ON — widgets are interactive again."
                    : "Mouse capture OFF — drag to select text and copy with your terminal (e.g. Ctrl+C or right-click). Press F12 to resume.",
                on ? NotificationSeverity.Success : NotificationSeverity.Warning,
                4500);
        }

        private void CycleTheme()
        {
            _ThemeIndex = (_ThemeIndex + 1) % 3;
            if (_ThemeIndex == 1)
                _App.Theme = Theme.Light;
            else if (_ThemeIndex == 2)
                _App.Theme = Theme.HighContrast;
            else
                _App.Theme = Theme.Dark;

            Log("Ctrl+T was pressed, theme is now " + _App.Theme.Name);
            _App.Notify("Theme: " + _App.Theme.Name, NotificationSeverity.Success, 2000);
        }

        private async void OpenSettings()
        {
            Log("Ctrl+G was pressed, opening the settings menu");
            int choice = await _App.SelectAsync(
                "Settings & actions",
                "Cycle theme (dark / light / high-contrast)",
                "Cycle icon mode (Unicode / ASCII / Nerd)",
                "Show a notification",
                "Confirmation dialog",
                "Help").ConfigureAwait(false);

            switch (choice)
            {
                case 0:
                    CycleTheme();
                    break;
                case 1:
                    CycleIconMode();
                    break;
                case 2:
                    _App.Notify("Settings applied.", NotificationSeverity.Success, 2000);
                    break;
                case 3:
                    ConfirmDemo();
                    break;
                case 4:
                    _ShowHelp = true;
                    break;
                default:
                    break;
            }
        }

        private void CycleIconMode()
        {
            if (Icons.Mode == IconMode.Unicode)
                Icons.Mode = IconMode.Ascii;
            else if (Icons.Mode == IconMode.Ascii)
                Icons.Mode = IconMode.Nerd;
            else
                Icons.Mode = IconMode.Unicode;

            _App.Notify("Icon mode: " + Icons.Mode + "  " + Icons.Star, NotificationSeverity.Info, 2000);
        }

        private async void ConfirmDemo()
        {
            bool ok = await _App.ConfirmAsync("Enable experimental sixel image output?", "Enable", "Cancel").ConfigureAwait(false);
            _App.Notify(ok ? "Sixel output enabled." : "Left disabled.", ok ? NotificationSeverity.Success : NotificationSeverity.Warning, 2500);
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
        /// <param name="showHelp">When true, overlays the help panel (for screenshots).</param>
        /// <returns>The frame as text.</returns>
        internal string RenderFrame(int width, int height, int pageIndex = 0, bool showHelp = false)
        {
            // A negative page index preserves the current state (e.g. after feeding keys).
            if (pageIndex >= 0)
            {
                _Index = ((pageIndex % _Pages.Count) + _Pages.Count) % _Pages.Count;
                _ShowHelp = showHelp;
            }

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
            // While a modal dialog is open, let the host route keys to it.
            if (_App.Modals.Count > 0)
                return;

            // Any key dismisses the help overlay.
            if (_ShowHelp)
            {
                _ShowHelp = false;
                return;
            }

            // Tab (and Shift+Tab) cycle focus between the navigable rectangles: Live demo and Interactive.
            if (key.Code == KeyCode.Tab)
            {
                bool back = (key.Modifiers & KeyModifiers.Shift) != 0;
                _FocusIndex = (_FocusIndex + (back ? FocusCount - 1 : 1)) % FocusCount;
                Log("Tab was pressed, focus is now " + FocusName);
                return;
            }

            // While the Interactive box is focused it receives every other key.
            if (_FocusIndex == FocusInteractive)
            {
                HandleInteractive(key);
                return;
            }

            // From here on the Live demo is focused.
            // PageUp/PageDown browse features — only meaningful while the Live demo is focused.
            if (key.Code == KeyCode.PageDown)
            {
                Navigate(1, "Page down");
                return;
            }

            if (key.Code == KeyCode.PageUp)
            {
                Navigate(-1, "Page up");
                return;
            }

            // Let the focusable demo consume the key first, so typing (e.g. in the fuzzy finder) works
            // instead of triggering navigation. Only keys the widget ignores fall through below.
            if (_Pages[_Index].Demo is IFocusable focusable && focusable.HandleKey(key))
            {
                Log(Describe(key) + " sent to the " + _Pages[_Index].Title + " demo");
                return;
            }

            // '[' / ']' also browse, for pages whose widget did not use them.
            if (IsChar(key, ']'))
                Navigate(1, "]");
            else if (IsChar(key, '['))
                Navigate(-1, "[");
        }

        private void HandleInteractive(KeyEvent key)
        {
            if (key.Code == KeyCode.Character && (key.Modifiers & KeyModifiers.Ctrl) == 0)
            {
                _Input += char.ConvertFromUtf32(key.Rune);
            }
            else if (key.Code == KeyCode.Backspace && _Input.Length > 0)
            {
                _Input = _Input.Substring(0, _Input.Length - 1);
            }
            else if (key.Code == KeyCode.Enter)
            {
                Log("Interactive: you typed \"" + _Input + "\"");
                _Input = string.Empty;
            }
        }

        private string FocusName
        {
            get { return _FocusIndex == FocusInteractive ? "Interactive" : "Live demo"; }
        }

        private void Navigate(int delta, string cause)
        {
            _Index = (_Index + delta + _Pages.Count) % _Pages.Count;
            Log(cause + " was pressed, navigating to " + _Pages[_Index].Title);
        }

        private static bool IsChar(KeyEvent key, char c)
        {
            return key.Code == KeyCode.Character && key.Rune == c;
        }

        private void Log(string message)
        {
            lock (_EventsLock)
            {
                _Events.Add(message);
                const int Max = 200;
                if (_Events.Count > Max)
                    _Events.RemoveRange(0, _Events.Count - Max);
            }
        }

        private static string Describe(KeyEvent key)
        {
            if (key.Code == KeyCode.Character)
                return "'" + char.ConvertFromUtf32(key.Rune) + "'";

            return key.Code.ToString();
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

            Color blackColor = Color.FromRgb(0, 0, 0);
            CellStyle black = CellStyle.Default.WithBackground(blackColor);
            Cell blackCell = Cell.Blank(black);

            // Fill the full interior of each box (inside the border, including the padding ring) with
            // black so nothing shows the theme background.
            FillInterior(buffer, "demo", size, blackCell);
            FillInterior(buffer, "code", size, blackCell);
            FillInterior(buffer, "interactive", size, blackCell);
            FillInterior(buffer, "actions", size, blackCell);

            // Highlight the border of whichever navigable rectangle is focused.
            DrawFocusBorder(root, "demo", "Live demo", size, _FocusIndex == FocusLiveDemo);
            DrawFocusBorder(root, "interactive", "Interactive", size, _FocusIndex == FocusInteractive);

            Rect demoRect = _Layout.FindById("demo")!.ContentRect(size);
            if (!demoRect.IsEmpty)
                page.Demo.Render(new SolidBackgroundSurface(buffer.CreateView(demoRect), blackColor));

            Rect codeRect = _Layout.FindById("code")!.ContentRect(size);
            if (!codeRect.IsEmpty)
            {
                BufferSurface codeView = buffer.CreateView(codeRect);
                for (int i = 0; i < page.Code.Count && i < codeRect.Height; i++)
                    codeView.DrawStyledText(0, i, SyntaxHighlighter.HighlightLine(page.Code[i], "csharp"), black);
            }

            DrawInteractive(buffer, size, black);
            DrawActions(buffer, size, black);

            CellStyle muted = _App.Theme.Muted;
            root.Fill(new Rect(0, size.Height - 1, size.Width, 1), Cell.Blank(muted));
            root.DrawText(0, size.Height - 1, " Tab: focus [" + FocusName + "]   PgUp/PgDn: browse   arrows/Enter: interact   ^G settings   F1 help   ^Q quit", muted);

            if (_ShowHelp)
                DrawHelp(root, size, buffer);
        }

        private void FillInterior(BufferSurface buffer, string regionId, Size size, Cell cell)
        {
            Region? region = _Layout.FindById(regionId);
            if (region == null)
                return;

            Rect full = region.Resolve(size);
            if (full.Width <= 2 || full.Height <= 2)
                return;

            buffer.Fill(new Rect(full.X + 1, full.Y + 1, full.Width - 2, full.Height - 2), cell);
        }

        private void DrawFocusBorder(ISurface root, string regionId, string title, Size size, bool focused)
        {
            Region? region = _Layout.FindById(regionId);
            if (region == null)
                return;

            Rect rect = region.Resolve(size);
            if (rect.Width < 2 || rect.Height < 2)
                return;

            CellStyle style = focused
                ? _App.Theme.Border.WithForeground(Color.FromPalette(6)).WithAttribute(CellAttributes.Bold, true)
                : _App.Theme.Border;
            root.DrawBox(rect, style, BorderStyle.Rounded, title);
        }

        private void DrawInteractive(BufferSurface buffer, Size size, CellStyle black)
        {
            Rect rect = _Layout.FindById("interactive")!.ContentRect(size);
            if (rect.IsEmpty)
                return;

            bool focused = _FocusIndex == FocusInteractive;
            BufferSurface view = buffer.CreateView(rect);

            string cursor = focused ? "▊" : "";
            CellStyle promptStyle = black.WithForeground(Color.FromPalette((byte)(focused ? 6 : 7)));
            view.DrawText(0, 0, "Type here: " + _Input + cursor, promptStyle);

            if (rect.Height > 1)
                view.DrawText(0, 1, "Echo: " + _Input, black.WithForeground(Color.FromPalette(2)));

            if (!focused && rect.Height > 2)
                view.DrawText(0, 2, "(press Tab to focus this box)", black.WithForeground(Color.FromPalette(8)));
        }

        private void DrawActions(BufferSurface buffer, Size size, CellStyle black)
        {
            Rect rect = _Layout.FindById("actions")!.ContentRect(size);
            if (rect.IsEmpty)
                return;

            string[] events;
            lock (_EventsLock)
                events = _Events.ToArray();

            BufferSurface view = buffer.CreateView(rect);
            if (events.Length == 0)
            {
                view.DrawText(0, 0, "Press PgUp / PgDn to navigate — key events are logged here.", black.WithForeground(Color.FromPalette(8)));
                return;
            }

            int start = Math.Max(0, events.Length - rect.Height);
            for (int row = 0; start + row < events.Length && row < rect.Height; row++)
            {
                string text = events[start + row];
                if (text.Length > rect.Width)
                    text = text.Substring(0, rect.Width);

                bool newest = start + row == events.Length - 1;
                CellStyle style = newest
                    ? black.WithForeground(Color.FromPalette(6)).WithAttribute(CellAttributes.Bold, true)
                    : black.WithForeground(Color.FromPalette(7));
                view.DrawText(0, row, text, style);
            }
        }

        private void DrawHelp(ISurface root, Size size, BufferSurface buffer)
        {
            string[] lines =
            {
                "TUIKit guided tour — keys",
                "",
                "  Tab              switch focus: Live demo / Interactive",
                "  PgUp / PgDn      previous / next feature (Live demo focus)",
                "  [  /  ]          previous / next feature (Live demo focus)",
                "  Up/Down/Enter    interact with the focused box",
                "  Ctrl+G           settings & actions menu",
                "  Ctrl+T           cycle theme (dark/light/high-contrast)",
                "  Ctrl+K           confirmation dialog demo",
                "  Ctrl+N           show a notification toast",
                "  F12              toggle mouse capture (native text select)",
                "  F1               toggle this help",
                "  Ctrl+Q           quit",
                "",
                "  With mouse capture OFF, drag to select and copy",
                "  with your terminal, then paste elsewhere.",
                "",
                "  Press any key to close"
            };

            int contentWidth = 0;
            for (int i = 0; i < lines.Length; i++)
                contentWidth = Math.Max(contentWidth, lines[i].Length);

            int width = Math.Min(size.Width, contentWidth + 4);
            int height = Math.Min(size.Height, lines.Length + 2);
            int x = Math.Max(0, (size.Width - width) / 2);
            int y = Math.Max(0, (size.Height - height) / 2);
            Rect box = new Rect(x, y, width, height);

            root.DrawShadow(box);
            root.Fill(box, Cell.Blank(CellStyle.Default.WithBackground(Color.FromPalette(0))));
            root.DrawBox(box, CellStyle.Default.WithForeground(Color.FromPalette(3)), BorderStyle.Rounded, "Help");

            BufferSurface inner = buffer.CreateView(new Rect(x + 2, y + 1, width - 4, height - 2));
            for (int i = 0; i < lines.Length && i < height - 2; i++)
                inner.DrawText(0, i, lines[i], CellStyle.Default);
        }

        private static List<TourPage> BuildPages()
        {
            List<TourPage> pages = new List<TourPage>();

            Pane welcome = new Pane("welcome");
            welcome.WriteMarkup("[bold]Welcome to the TUIKit tour.[/]");
            welcome.WriteLine(string.Empty);
            welcome.WriteMarkup("Browse features with [yellow]PgUp/PgDn[/].");
            welcome.WriteMarkup("[yellow]Tab[/] switches focus: [green]Live demo[/] / [green]Interactive[/].");
            welcome.WriteLine(string.Empty);
            welcome.WriteMarkup("Try the live [green]modals[/] and [green]settings[/]:");
            welcome.WriteMarkup("  [yellow]Ctrl+G[/]  settings & actions menu");
            welcome.WriteMarkup("  [yellow]Ctrl+K[/]  confirmation dialog");
            welcome.WriteMarkup("  [yellow]Ctrl+N[/]  notification toast");
            welcome.WriteMarkup("  [yellow]Ctrl+T[/]  cycle light / dark theme");
            welcome.WriteMarkup("  [yellow]F1[/]      help overlay");
            pages.Add(new TourPage(
                "Welcome — modals, settings & help",
                "Global keys open live UI. Press [bold]Ctrl+G[/] for settings or [bold]F1[/] for help now.",
                welcome,
                new[]
                {
                    "// Modal dialogs and toasts:",
                    "bool ok = await app.ConfirmAsync(",
                    "  \"Enable sixel output?\");",
                    "int pick = await app.SelectAsync(",
                    "  \"Theme\", \"Dark\", \"Light\");",
                    "app.Notify(\"Saved\",",
                    "  NotificationSeverity.Success);",
                    "app.Theme = Theme.Light;"
                }));

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

            double[] waveA = new double[48];
            double[] waveB = new double[48];
            for (int i = 0; i < waveA.Length; i++)
            {
                waveA[i] = Math.Sin(i * 0.30);
                waveB[i] = Math.Sin(i * 0.18 + 1.0) * 0.8;
            }

            pages.Add(new TourPage(
                "Line chart (braille)",
                "[bold]LineChart[/] plots a series onto a [bold]BrailleCanvas[/] — here two waves in two colors.",
                new DualLineChart(waveA, Color.FromPalette(6), waveB, Color.FromPalette(5)),
                new[]
                {
                    "LineChart a = new LineChart(waveA);",
                    "a.Color = Color.FromPalette(6);  // cyan",
                    "LineChart b = new LineChart(waveB);",
                    "b.Color = Color.FromPalette(5);  // magenta",
                    "a.Render(surface);",
                    "b.Render(surface); // overlay"
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
                    "ProgressTask t = p.Add(\"download\");",
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
                    "FuzzyList list = new FuzzyList(items);",
                    "// typing filters:",
                    "list.Query = \"ap\";",
                    "string? pick = list.SelectedItem;"
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
                    "MenuBar bar = new MenuBar();",
                    "bar.AddMenu(\"File\")",
                    "   .Add(\"Open\", OpenFile)",
                    "   .Add(\"Quit\", app.Quit);"
                }));

            SplitView split = new SplitView(
                SplitOrientation.Vertical,
                new SplitView(SplitOrientation.Horizontal,
                    new Label(Text.From("This is the top-left rectangle")),
                    new Label(Text.From("This is the top-right rectangle"))),
                new Label(Text.From("This is the bottom rectangle")));
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
                    "ColorPicker picker = new ColorPicker(",
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
                    "KeyBindingSet set = new KeyBindingSet()",
                    "  .Add(\"save\", \"ctrl+s\")",
                    "  .Add(\"quit\", \"ctrl+q\");",
                    "KeyBindingEditor editor =",
                    "  new KeyBindingEditor(set);"
                }));

            pages.Add(new TourPage(
                "Images (half-block / sixel / kitty)",
                "[bold]HalfBlockImage[/] works everywhere; [bold]SixelEncoder[/]/[bold]KittyImageEncoder[/] target capable terminals.",
                new HalfBlockImage(40, 32, (x, y) => Color.FromRgb((byte)(x * 6), (byte)(y * 7), 160)),
                new[]
                {
                    "// Portable: draws into the cell grid",
                    "new HalfBlockImage(w, h,",
                    "  (x, y) => Color.FromRgb(r, g, b));",
                    "",
                    "// Capable terminals: raw escapes",
                    "string s = SixelEncoder.Encode(px);",
                    "string k = KittyImageEncoder.Encode(px);",
                    "backend.Write(s); // at the cursor"
                }));

            return pages;
        }
    }
}

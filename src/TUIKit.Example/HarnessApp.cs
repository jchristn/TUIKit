namespace TUIKit.Example
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Diagnostics;
    using TUIKit.Hosting;
    using TUIKit.Input;
    using TUIKit.Layout;
    using TUIKit.Modals;
    using TUIKit.Widgets;

    /// <summary>
    /// The agent control harness demo. Wires a multi-region layout, a streaming transcript, a live
    /// tool panel and telemetry, a multi-line composer, command routing, modals, notifications, links,
    /// theming, and diagnostic overlays into one application. A help overlay (F1 or ?) lists every
    /// keybinding, so the demo documents itself.
    /// </summary>
    internal sealed class HarnessApp
    {
        private readonly TuiApplication _App;
        private readonly Pane _Transcript = new Pane("transcript");
        private readonly Pane _Tools = new Pane("tools");
        private readonly TextEditor _Composer = new TextEditor();
        private readonly Gauge _CpuGauge = new Gauge();
        private readonly Sparkline _CpuSpark = new Sparkline();
        private readonly ProgressBar _ToolBar = new ProgressBar();
        private readonly Table _Telemetry = new Table(new[] { "Metric", "Value" });
        private readonly FrameStats _Stats = new FrameStats();
        private readonly LinkRegistry _Links = new LinkRegistry();
        private readonly HarnessState _State;
        private readonly SimulatedAgent _Agent;
        private readonly Layout _Layout;
        private bool _ShowHelp;
        private bool _ShowDebug;
        private int _ThemeIndex;
        private long _LastFrameMs;

        internal HarnessApp(TuiApplication app)
        {
            _App = app ?? throw new ArgumentNullException(nameof(app));
            _State = new HarnessState(app.Notifications, () => app.NowMilliseconds);
            _Agent = new SimulatedAgent(_Transcript, _Tools, _State);

            // Every rectangle keeps one cell of interior padding on all four sides by default, so
            // content never touches a border and the debug overlay (Ctrl+D) draws its outline in the
            // padding gap rather than over content. The single-row header and footer bars override the
            // vertical padding to zero; the composer widens its padding to leave a gap inside its border.
            // Regions declare whether they draw a border and what kind. The tool, telemetry, and
            // composer panels use rounded box-drawing borders; the header and footer are borderless
            // single-row bars. The host draws the border and insets content for it automatically.
            _Layout = Layout.Create()
                .Add("header", r => r.FillWidth().TopAnchored(0, 1).WithPadding(1, 0, 1, 0))
                .Add("transcript", r => r.Horizontal(AxisConstraint.Stretch(0, 34)).Vertical(AxisConstraint.Stretch(1, 6)))
                .Add("tools", r => r.RightAnchored(0, 33).TopAnchored(1, 11).WithBorder(BorderStyle.Rounded, "Tools"))
                .Add("telemetry", r => r.RightAnchored(0, 33).Vertical(AxisConstraint.Stretch(12, 6)).WithBorder(BorderStyle.Rounded, "Telemetry"))
                .Add("composer", r => r.FillWidth().BottomAnchored(1, 4).WithBorder(BorderStyle.Rounded, "Composer  (Enter to send)"))
                .Add("footer", r => r.FillWidth().BottomAnchored(0, 1).WithPadding(1, 0, 1, 0))
                .Build();

            _App.Layout = _Layout;
            _App.BindPane("transcript", _Transcript);
            _App.BindPane("tools", _Tools);
            _App.CtrlCPolicy = CtrlCPolicy.DoubleTapToExit;
            _App.FocusContext = "composer";
            _Composer.IsFocused = true;
            _Telemetry.AddRow(new[] { "cpu", "0%" });
            _Telemetry.AddRow(new[] { "tool", "idle" });

            RegisterCommands();
            _App.KeyReceived += OnKey;
            _App.PasteReceived += text => _Composer.InsertText(text);
            _App.MouseReceived += OnMouse;
            _App.RenderOverlay = DrawOverlay;

            // Toasts are drawn by the app into the transcript area so they never cover the header
            // bar or the fixed tool/telemetry column on the right.
            _App.AutoRenderNotifications = false;
        }

        internal void StartLive()
        {
            _App.Start();
            _Agent.Start();
        }

        internal void StopLive()
        {
            _Agent.Stop();
            _App.Stop();
        }

        /// <summary>
        /// Seeds a burst of content and returns a text snapshot of a single rendered frame. Used for
        /// the headless, deterministic demo and README screenshot.
        /// </summary>
        /// <param name="width">The frame width.</param>
        /// <param name="height">The frame height.</param>
        /// <returns>The frame as text.</returns>
        internal string RenderSeededFrame(int width, int height, bool debug = false, bool help = false, bool confirm = false, bool light = false)
        {
            if (light)
                _App.Theme = Theming.Theme.Light;

            _ShowDebug = debug;
            _ShowHelp = help;
            _Agent.SeedOnce();
            _App.Notifications.Add("Tool finished: dotnet test", Modals.NotificationSeverity.Info, 0, 0);
            if (confirm)
                OpenConfirmation();

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

            _Transcript.Render(surface.CreateView(RegionFor("transcript").ContentRect(buffer.Size)), _App.Theme.Text);
            _Tools.Render(surface.CreateView(RegionFor("tools").ContentRect(buffer.Size)), _App.Theme.Text);
            DrawOverlay(surface);
            _App.Modals.Render(surface);
            return TUIKit.Testing.Snapshot.ToText(buffer);
        }

        private Region RegionFor(string id)
        {
            Region? region = _Layout.FindById(id);
            if (region == null)
                throw new InvalidOperationException("Region not found: " + id);

            return region;
        }

        private void RegisterCommands()
        {
            _App.Commands.Register(KeyChord.Parse("ctrl+q"), "quit");
            _App.Commands.Register(KeyChord.Parse("f1"), "help");
            _App.Commands.Register(KeyChord.Parse("?"), "help");
            _App.Commands.Register(KeyChord.Parse("ctrl+p"), "palette");
            _App.Commands.Register(KeyChord.Parse("ctrl+g"), "settings");
            _App.Commands.Register(KeyChord.Parse("ctrl+d"), "debug");
            _App.Commands.Register(KeyChord.Parse("ctrl+l"), "confirm");
            _App.Commands.RegisterSequence(KeyChord.Parse("ctrl+k"), KeyChord.Parse("ctrl+t"), "theme");

            _App.RegisterCommand("quit", () => _App.RequestStop());
            _App.RegisterCommand("help", () => _ShowHelp = !_ShowHelp);
            _App.RegisterCommand("debug", () => _ShowDebug = !_ShowDebug);
            _App.RegisterCommand("theme", CycleTheme);
            _App.RegisterCommand("palette", OpenPalette);
            _App.RegisterCommand("settings", OpenSettings);
            _App.RegisterCommand("confirm", OpenConfirmation);
        }

        private void OnKey(KeyEvent key)
        {
            if (key.Code == KeyCode.PageUp)
            {
                _Transcript.ScrollUp(4);
                return;
            }

            if (key.Code == KeyCode.PageDown)
            {
                _Transcript.ScrollDown(4);
                return;
            }

            if (key.Code == KeyCode.Enter && (key.Modifiers & KeyModifiers.Alt) == 0)
            {
                Submit();
                return;
            }

            _Composer.HandleKey(key);
        }

        private void Submit()
        {
            string message = _Composer.Text.Trim();
            if (message.Length == 0)
                return;

            _Transcript.WriteLine(Text.From("you").Green().Bold().Append(Text.From("  " + message)));
            _Composer.Text = string.Empty;
            _App.Notifications.Add("Message sent", NotificationSeverity.Info, _App.NowMilliseconds, 2000);
        }

        private void OnMouse(MouseEvent mouse)
        {
            if (mouse.Button == MouseButton.WheelUp)
                _Transcript.ScrollUp(3);
            else if (mouse.Button == MouseButton.WheelDown)
                _Transcript.ScrollDown(3);
            else if (mouse.Kind == MouseEventKind.Press)
            {
                Link? link = _Links.HitTest(mouse.X, mouse.Y);
                if (link != null)
                    _App.Notifications.Add("Open link: " + link.Uri, NotificationSeverity.Info, _App.NowMilliseconds, 3000);
            }
        }

        private void CycleTheme()
        {
            _ThemeIndex = (_ThemeIndex + 1) % 3;
            switch (_ThemeIndex)
            {
                case 1:
                    _App.Theme = Theming.Theme.Light;
                    break;
                case 2:
                    _App.Theme = Theming.Theme.HighContrast;
                    break;
                default:
                    _App.Theme = Theming.Theme.Dark;
                    break;
            }

            _App.Notifications.Add("Theme: " + _App.Theme.Name, NotificationSeverity.Info, _App.NowMilliseconds, 2000);
        }

        private void OpenPalette()
        {
            ChoiceModal palette = new ChoiceModal("Command Palette", new List<string> { "Help", "Settings", "Cycle theme", "Toggle debug", "Quit" });
            _App.Modals.Push(palette);
            palette.Completion.ContinueWith(task =>
            {
                int index = task.Result is int value ? value : -1;
                switch (index)
                {
                    case 0:
                        _ShowHelp = true;
                        break;
                    case 1:
                        OpenSettings();
                        break;
                    case 2:
                        CycleTheme();
                        break;
                    case 3:
                        _ShowDebug = !_ShowDebug;
                        break;
                    case 4:
                        _App.RequestStop();
                        break;
                    default:
                        break;
                }
            }, System.Threading.Tasks.TaskScheduler.Default);
        }

        private void OpenSettings()
        {
            SettingsModal settings = new SettingsModal(_App.Theme.Name);
            _App.Modals.Push(settings);
            settings.Completion.ContinueWith(task =>
            {
                if (task.Result is SettingsResult result)
                {
                    if (string.Equals(result.Theme, "Light", StringComparison.Ordinal))
                        _App.Theme = Theming.Theme.Light;
                    else if (string.Equals(result.Theme, "HighContrast", StringComparison.Ordinal))
                        _App.Theme = Theming.Theme.HighContrast;
                    else
                        _App.Theme = Theming.Theme.Dark;

                    _App.Notifications.Add("Settings applied", NotificationSeverity.Success, _App.NowMilliseconds, 2000);
                }
            }, System.Threading.Tasks.TaskScheduler.Default);
        }

        private void OpenConfirmation()
        {
            MessageModal confirm = new MessageModal(
                "Allow tool call?",
                "The agent wants to run `rm -rf build/`. Allow this?",
                new List<string> { "Allow", "Deny" });
            _App.Modals.Push(confirm);
            confirm.Completion.ContinueWith(task =>
            {
                int index = task.Result is int value ? value : -1;
                NotificationSeverity severity = index == 0 ? NotificationSeverity.Warning : NotificationSeverity.Info;
                string message = index == 0 ? "Tool call allowed" : "Tool call denied";
                _App.Notifications.Add(message, severity, _App.NowMilliseconds, 3000);
            }, System.Threading.Tasks.TaskScheduler.Default);
        }

        private void DrawOverlay(ISurface root)
        {
            long now = _App.NowMilliseconds;
            _Stats.Record(Math.Max(0, now - _LastFrameMs));
            _LastFrameMs = now;

            Size size = root.Size;
            if (!_Layout.FitsIn(size))
                return;

            BufferSurface? buffer = root as BufferSurface;
            if (buffer == null)
                return;

            UpdateTelemetry();
            DrawHeader(root, size);
            DrawTelemetry(buffer);
            DrawComposer(buffer);
            DrawFooter(root, size);
            DrawNotifications(buffer, now);

            if (_ShowDebug)
                DebugOverlay.Render(root, _Layout, _Stats);

            if (_ShowHelp)
                DrawHelp(root, size);
        }

        private void UpdateTelemetry()
        {
            _CpuGauge.Value = _State.CurrentCpu();
            _CpuSpark.SetValues(_State.CpuSnapshot());
            _ToolBar.Value = _State.ToolProgress;
            _Telemetry.ClearRows();
            _Telemetry.AddRow(new[] { "cpu", ((int)(_State.CurrentCpu() * 100)) + "%" });
            _Telemetry.AddRow(new[] { "tool", _State.ActiveTool ? "running" : "idle" });
        }

        private void DrawHeader(ISurface root, Size size)
        {
            CellStyle bar = CellStyle.Default.WithForeground(Color.FromRgb(0, 0, 0)).WithBackground(Color.FromPalette(6));
            Rect content = RegionFor("header").ContentRect(size);
            root.Fill(new Rect(0, 0, size.Width, 1), Cell.Blank(bar));
            root.DrawText(content.X, 0, "TUIKit Agent Harness", bar);

            string linkLabel = "[docs]";
            int linkX = content.Right - linkLabel.Length;
            _Links.Clear();
            _Links.Add(linkLabel, new Rect(linkX, 0, linkLabel.Length, 1), "https://github.com/jchristn/TUIKit");
            root.DrawText(linkX, 0, linkLabel, bar.WithAttribute(CellAttributes.Underline, true));
        }

        private void DrawTelemetry(BufferSurface buffer)
        {
            Rect rect = RegionFor("telemetry").ContentRect(buffer.Size);
            if (rect.IsEmpty)
                return;

            // The "Telemetry" title is drawn on the region border; the widgets fill the content area.
            BufferSurface view = buffer.CreateView(rect);
            _CpuGauge.Render(view.CreateView(new Rect(0, 0, rect.Width, 1)));
            _CpuSpark.Render(view.CreateView(new Rect(0, 1, rect.Width, 1)));
            _ToolBar.Render(view.CreateView(new Rect(0, 2, rect.Width, 1)));
            if (rect.Height > 4)
                _Telemetry.Render(view.CreateView(new Rect(0, 4, rect.Width, rect.Height - 4)));
        }

        private void DrawComposer(BufferSurface buffer)
        {
            // The composer region declares its own border (drawn by the host); the editor just renders
            // into the region's content rectangle, which is already inset for the border and padding.
            Rect content = RegionFor("composer").ContentRect(buffer.Size);
            if (!content.IsEmpty)
                _Composer.Render(buffer.CreateView(content));
        }

        private void DrawFooter(ISurface root, Size size)
        {
            CellStyle muted = _App.Theme.Muted;
            string detached = _Transcript.IsAtBottom ? "live" : "detached " + _Transcript.NewSinceDetached + " new";
            string footer = Hint("f1") + " help  "
                + Hint("ctrl+p") + " palette  "
                + Hint("ctrl+g") + " settings  "
                + Hint("ctrl+k") + Hint("ctrl+t") + " theme  "
                + Hint("ctrl+d") + " debug  "
                + Hint("ctrl+l") + " confirm  "
                + Hint("ctrl+q") + " quit   [" + detached + "]";
            Rect content = RegionFor("footer").ContentRect(size);
            root.Fill(new Rect(0, size.Height - 1, size.Width, 1), Cell.Blank(muted));
            root.DrawText(content.X, size.Height - 1, footer, muted);
        }

        // Renders a key chord using the label conventions of the current environment: ⌃G on macOS,
        // Ctrl+G on Windows and Linux.
        private static string Hint(string chord)
        {
            return KeyChord.Parse(chord).ToLabel(KeyLabel.Recommended);
        }

        private void DrawNotifications(BufferSurface buffer, long now)
        {
            // Render toasts inside the transcript region (top-right of that pane), which begins below
            // the header and to the left of the tool/telemetry column, so they never cover either.
            Rect rect = RegionFor("transcript").ContentRect(buffer.Size);
            if (rect.IsEmpty)
                return;

            _App.Notifications.Render(buffer.CreateView(rect), now);
        }

        private void DrawHelp(ISurface root, Size size)
        {
            string[] lines =
            {
                "Keybindings",
                "",
                "  " + Hint("enter").PadRight(13) + "send composer message",
                "  " + Hint("alt+enter").PadRight(13) + "newline in composer",
                "  " + (Hint("pageup") + "/" + Hint("pagedown")).PadRight(13) + "scroll transcript (smart lock)",
                "  " + Hint("ctrl+p").PadRight(13) + "command palette",
                "  " + Hint("ctrl+g").PadRight(13) + "settings (widgets + tab order)",
                "  " + (Hint("ctrl+k") + " " + Hint("ctrl+t")).PadRight(13) + "cycle theme (multi-key chord)",
                "  " + Hint("ctrl+d").PadRight(13) + "toggle debug overlay",
                "  " + Hint("ctrl+l").PadRight(13) + "tool-call confirmation modal",
                "  " + Hint("ctrl+c").PadRight(13) + "double-tap to exit (policy)",
                "  " + Hint("ctrl+q").PadRight(13) + "quit",
                "  " + (Hint("f1") + " / ?").PadRight(13) + "toggle this help"
            };

            // Size the box to the longest line, add one cell of padding on every side, and render the
            // lines into a clipped view so nothing spills outside the modal.
            const int padLeft = 1;
            const int padTop = 1;
            int contentWidth = 0;
            for (int i = 0; i < lines.Length; i++)
                contentWidth = Math.Max(contentWidth, TUIKit.Unicode.Graphemes.MeasureWidth(lines[i]));

            int width = Math.Min(size.Width, contentWidth + 2 + (padLeft * 2));
            int height = Math.Min(size.Height, lines.Length + 2 + (padTop * 2));
            int x = Math.Max(0, (size.Width - width) / 2);
            int y = Math.Max(0, (size.Height - height) / 2);
            Rect box = new Rect(x, y, width, height);

            root.Fill(box, Cell.Blank(CellStyle.Default));
            root.DrawBox(box, CellStyle.Default.WithForeground(Color.FromPalette(3)), "Help");

            BufferSurface? buffer = root as BufferSurface;
            if (buffer == null)
                return;

            BufferSurface inner = buffer.CreateView(new Rect(x + 1 + padLeft, y + 1 + padTop, width - 2 - (padLeft * 2), height - 2 - (padTop * 2)));
            for (int i = 0; i < lines.Length; i++)
                inner.DrawText(0, i, lines[i], CellStyle.Default);
        }
    }
}

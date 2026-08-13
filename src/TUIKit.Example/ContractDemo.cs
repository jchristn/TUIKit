namespace TUIKit.Example
{
    using System;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Hosting;
    using TUIKit.Input;
    using TUIKit.Layout;
    using TUIKit.Modals;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    /// <summary>
    /// A compact showcase of the v0.4.0 interaction contract. Where the older harness re-assembles the
    /// interactive skeleton by hand, this screen is "bind widgets, set focus, run": a four-way dock
    /// shell (header, footer, sidebar, main) built from real regions, a host-owned focus ring across the
    /// sidebar list and the editor, click-to-focus and wheel routing for free, a focus-scoped Enter that
    /// opens a file in the sidebar while Enter in the editor still inserts a newline, a two-key theme
    /// chord, and a typed modal whose result is marshalled back onto the loop with <c>Post</c>.
    /// </summary>
    internal sealed class ContractDemo
    {
        private static readonly string[] _FileNames = { "readme.md", "main.cs", "notes.txt" };

        private static readonly string[] _FileBodies =
        {
            "# TUIKit\n\nA concurrency-first terminal UI framework for .NET.\n\nClick a pane to focus it, or press Tab to cycle focus.",
            "using TUIKit.Hosting;\n\n// The whole app:\n//   1. dock the shell\n//   2. bind widgets\n//   3. set focus\n//   4. run",
            "- Click any pane to focus it (host hit-testing)\n- Tab / Shift+Tab cycles the focus ring\n- Enter here in the list opens a file\n- Ctrl+O opens a typed picker modal\n- Ctrl+K Ctrl+T cycles the theme"
        };

        private readonly TuiApplication _App;
        private readonly Label _Header = new Label(Text.From("TUIKit — v0.4.0 interaction contract"));
        private readonly ListView<string> _Files = new ListView<string>();
        private readonly TextEditor _Editor = new TextEditor();
        private readonly StatusBar _Status = new StatusBar();
        private bool _Dark = true;

        internal ContractDemo(TuiApplication app)
        {
            _App = app ?? throw new ArgumentNullException(nameof(app));
            Build();
        }

        internal void Start()
        {
            _App.Start();
        }

        internal void Stop()
        {
            _App.Stop();
        }

        private void Build()
        {
            // A real four-way application shell — no hand-computed rectangles, no overlay math.
            _App.Layout = Layout.Create()
                .DockTop("header", 1)
                .DockBottom("status", 1)
                .DockLeft("files", 22)
                .Fill("editor")
                .Build();

            _Files.SetItems(_FileNames);
            _Editor.Text = _FileBodies[0];

            _Status
                .Add("Tab", "Focus")
                .Add("Enter", "Open")
                .Add("^O", "Pick")
                .Add("^K ^T", "Theme")
                .Add("^Q", "Quit");

            // Bind widgets into their regions. Focusable widgets (the list and the editor) join the host
            // focus ring automatically, in bind order.
            _App.Bind("header", _Header);
            _App.Bind("files", _Files);
            _App.Bind("editor", _Editor);
            _App.Bind("status", _Status);
            _App.Focus("files");

            // The header reflects which widget holds focus, so focus is always visible.
            _App.FocusChanged += UpdateHeader;
            UpdateHeader(_App.FocusedRegion);

            // Focus-scoped Enter opens the selected file — but only while the sidebar has focus. In the
            // editor, the same Enter falls through to the editor and inserts a newline. This is the
            // precedence chain doing its job.
            _App.RegisterCommand("contract.open", OpenSelected);
            _App.Commands.Register(KeyChord.Parse("enter"), "contract.open", CommandScope.FocusContext, "files");

            // A two-key sequence — the syntax the old Bind could not parse — cycles the theme.
            _App.Bind("ctrl+k ctrl+t", CycleTheme);

            // A typed modal whose result is applied on the loop thread via Post.
            _App.Bind("ctrl+o", OpenPicker);
            _App.Bind("ctrl+q", _App.Quit);
        }

        private void UpdateHeader(string? focusedRegion)
        {
            string where = focusedRegion == "editor" ? "editor" : "sidebar";
            _Header.Content = Text.From("TUIKit v0.4.0 — bind widgets, set focus, run     [focus: " + where + "]");
        }

        private void OpenSelected()
        {
            int index = _Files.SelectedIndex;
            if (index >= 0 && index < _FileBodies.Length)
            {
                _Editor.Text = _FileBodies[index];
                _App.Focus("editor");
                _App.Notify("Opened " + _FileNames[index], NotificationSeverity.Success, 2000);
            }
        }

        private void CycleTheme()
        {
            _Dark = !_Dark;
            _App.Theme = _Dark ? Theme.Dark : Theme.Light;
            _App.Notify("Theme: " + (_Dark ? "Dark" : "Light"), NotificationSeverity.Info, 1500);
        }

        private async void OpenPicker()
        {
            int? choice = await _App.ShowAsync<int>(new SelectModal("Open file", _FileNames)).ConfigureAwait(false);

            // The continuation resumes off the loop thread; Post marshals the UI mutation back onto it.
            _App.Post(() =>
            {
                if (choice is int index && index >= 0 && index < _FileBodies.Length)
                {
                    _Editor.Text = _FileBodies[index];
                    _App.Focus("editor");
                    _App.Notify("Opened " + _FileNames[index], NotificationSeverity.Success, 2000);
                }
            });
        }

        internal string RenderFrame(int width, int height)
        {
            // Compose the real layout into a buffer for a deterministic, readable snapshot.
            CellBuffer buffer = new CellBuffer(width, height);
            BufferSurface surface = new BufferSurface(buffer);
            surface.Fill(new Rect(0, 0, width, height), Cell.Blank(_App.Theme.Text));

            Layout layout = _App.Layout!;
            Size size = new Size(width, height);
            for (int i = 0; i < layout.Regions.Count; i++)
            {
                Region region = layout.Regions[i];
                Rect rect = region.ContentRect(size).Intersect(new Rect(0, 0, width, height));
                if (rect.IsEmpty)
                    continue;

                BufferSurface view = surface.CreateView(rect);
                view.Fill(new Rect(0, 0, rect.Width, rect.Height), Cell.Blank(_App.Theme.Text));
                RenderRegion(region.Id, view);
            }

            return TUIKit.Testing.Snapshot.ToText(buffer);
        }

        private void RenderRegion(string id, BufferSurface view)
        {
            switch (id)
            {
                case "header":
                    _Header.Render(view);
                    break;
                case "files":
                    _Files.Render(view);
                    break;
                case "editor":
                    _Editor.Render(view);
                    break;
                case "status":
                    _Status.Render(view);
                    break;
                default:
                    break;
            }
        }
    }
}

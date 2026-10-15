namespace TUIKit.Example
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using TUIKit;
    using TUIKit.Hosting;
    using TUIKit.Input;
    using TUIKit.Modals;
    using TUIKit.Widgets;

    // The "Forms and flows" tour page (1.9, C6 and C7): a settings Form with sections, hints, labels beside the
    // fields, errors under each field, Ctrl+S and Esc, Save and Cancel buttons, arrow keys between fields, and
    // snapshot dirty tracking shown live; F4 opens a three-step WizardModal with an optional step and a step that
    // an earlier answer skips. F5 to F9 switch the form's features.
    internal sealed class FormDemoWidget : IWidget, IFocusContainer, IFocusAware, IMouseAware, IKeyHintSource, ITextEntry, ITextEntryKeys, IPasteTarget
    {
        private static readonly FormValidationMode[] _Modes = { FormValidationMode.OnSubmit, FormValidationMode.OnLeave, FormValidationMode.OnChange };

        private readonly TuiApplication? _App;
        private readonly Form _Form = new Form();
        private readonly TextField _Name = new TextField();
        private readonly TextField _Prefix = new TextField();
        private readonly TextField _Port = new TextField();
        private readonly Checkbox _Telemetry = new Checkbox("Send anonymous usage data", false);
        private int _Mode;
        private int _Scroll;
        private string _Status = "Ctrl+S saves, Esc cancels, F4 opens the setup wizard.";

        internal FormDemoWidget(TuiApplication? app)
        {
            _App = app;
            _Name.Value = "armada";
            _Prefix.Value = "feature/";
            _Port.Value = "7890";
            _Form.AddSection("Project");
            _Form.Add("Name", _Name, () => _Name.Value.Trim().Length == 0 ? "A name is required" : null);
            _Form.Add("Branch prefix", _Prefix, () => _Prefix.Value.EndsWith("/", StringComparison.Ordinal) ? null : "End the prefix with /");
            _Form.SetFieldHint(1, "Used as the prefix for new work branches");
            _Form.AddSection("Server");
            _Form.Add("Port", _Port, () => int.TryParse(_Port.Value, out int port) && port > 0 && port < 65536 ? null : "A port from 1 to 65535");
            _Form.Add(string.Empty, _Telemetry);
            _Form.LabelPlacement = FormLabelPlacement.Left;
            _Form.LabelWidth = 17;
            _Form.ShowFieldErrors = true;
            _Form.ShowButtons = true;
            _Form.ArrowKeysMoveFocus = true;
            _Form.SubmitKey = KeyChord.Parse("ctrl+s");
            _Form.CancelKey = KeyChord.Parse("escape");
            _Form.DirtyTracking = FormDirtyTracking.Snapshot;
            _Form.WrapFocus = false;
            _Form.SubmitRequested += (s, e) =>
            {
                _Form.MarkClean();
                _Status = "Saved " + _Name.Value + " on port " + _Port.Value + ".";
            };
            _Form.CancelRequested += (s, e) => _Status = _Form.IsDirty ? "Cancel: your unsaved changes would be discarded (the app asks)." : "Cancel: nothing to discard.";
            _Form.DirtyChanged += dirty => _Status = dirty ? "Unsaved changes." : "No unsaved changes (a revert counts).";
        }

        public bool AcceptsText
        {
            get { return _Form.FocusedLeaf is ITextEntry entry && entry.AcceptsText; }
        }

        public KeyHint? LeaveHint
        {
            get { return null; }
        }

        public IFocusable? FocusedLeaf
        {
            get { return _Form.FocusedLeaf; }
        }

        public bool ConsumesChord(KeyChord chord)
        {
            if (chord.Code >= KeyCode.F4 && chord.Code <= KeyCode.F9)
                return true;
            if ((_Form.SubmitKey.HasValue && chord.Equals(_Form.SubmitKey.Value)) || (_Form.CancelKey.HasValue && chord.Equals(_Form.CancelKey.Value)))
                return true;
            return _Form.FocusedLeaf is ITextEntryKeys keys && keys.ConsumesChord(chord);
        }

        public bool HandlePaste(string text)
        {
            return _Form.HandlePaste(text);
        }

        public bool MoveFocus(bool forward)
        {
            return _Form.MoveFocus(forward);
        }

        public void FocusEdge(bool first)
        {
            _Form.FocusEdge(first);
        }

        public bool HandleKey(KeyEvent key)
        {
            switch (key.Code)
            {
                case KeyCode.F4:
                    OpenWizard();
                    return true;
                case KeyCode.F5:
                    _Form.LabelPlacement = _Form.LabelPlacement == FormLabelPlacement.Left ? FormLabelPlacement.Above : FormLabelPlacement.Left;
                    _Status = "Labels " + (_Form.LabelPlacement == FormLabelPlacement.Left ? "beside" : "above") + " the fields.";
                    return true;
                case KeyCode.F6:
                    _Form.ShowFieldErrors = !_Form.ShowFieldErrors;
                    _Status = "Field errors " + (_Form.ShowFieldErrors ? "shown" : "hidden") + ".";
                    return true;
                case KeyCode.F7:
                    _Mode = (_Mode + 1) % _Modes.Length;
                    _Form.ValidationMode = _Modes[_Mode];
                    _Status = "Validation " + _Modes[_Mode] + ".";
                    return true;
                case KeyCode.F8:
                    _Form.DirtyTracking = _Form.DirtyTracking == FormDirtyTracking.Snapshot ? FormDirtyTracking.Events : FormDirtyTracking.Snapshot;
                    _Status = "Dirty tracking " + _Form.DirtyTracking + (_Form.DirtyTracking == FormDirtyTracking.Snapshot ? ": a revert clears it." : ": any change sticks.");
                    return true;
                case KeyCode.F9:
                    _Form.ArrowKeysMoveFocus = !_Form.ArrowKeysMoveFocus;
                    _Status = "Up/Down between fields " + (_Form.ArrowKeysMoveFocus ? "on" : "off") + ".";
                    return true;
            }

            return _Form.HandleKey(key);
        }

        public IReadOnlyList<KeyHint>? GetKeyHints()
        {
            List<KeyHint> hints = new List<KeyHint>();
            IReadOnlyList<KeyHint>? own = _Form.GetKeyHints();
            if (own != null)
                hints.AddRange(own);
            hints.Add(new KeyHint("F4", "Wizard"));
            hints.Add(new KeyHint("F5-F9", "Settings"));
            return hints;
        }

        public void OnFocusChanged(bool focused)
        {
            _Form.OnFocusChanged(focused);
        }

        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            return _Form.HandleMouse(new MouseEvent(mouse.Kind, mouse.Button, mouse.X, mouse.Y - 2 + _Scroll, mouse.Modifiers, mouse.ClickCount));
        }

        public Size Measure(Size available)
        {
            return available;
        }

        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            int width = surface.Size.Width;
            int height = surface.Size.Height;
            if (width <= 0 || height <= 3)
                return;

            CellStyle muted = CellStyle.Default.WithForeground(Color.FromPalette(8));
            string flags = (_Form.IsDirty ? "* unsaved" : "  saved") + " | F5 labels:" + _Form.LabelPlacement + " F6 errors:" + (_Form.ShowFieldErrors ? "on" : "off")
                + " F7 " + _Form.ValidationMode + " F8 " + _Form.DirtyTracking + " F9 arrows:" + (_Form.ArrowKeysMoveFocus ? "on" : "off");
            surface.DrawText(0, 0, TUIKit.Unicode.TextFit.Ellipsize(flags, width), _Form.IsDirty ? CellStyle.Default.WithForeground(Color.FromPalette(3)) : muted);
            surface.DrawText(0, 1, TUIKit.Unicode.TextFit.Ellipsize(_Status, width), CellStyle.Default.WithForeground(Color.FromPalette(2)));
            // The form is taller than the demo box, so render it whole off screen and show the window around the
            // focused field (the same job a ScrollView does through IScrollExtent).
            int view = height - 2;
            CellBuffer buffer = new CellBuffer(width, Math.Max(view, 40));
            _Form.Render(new BufferSurface(buffer));
            int total = Math.Max(view, _Form.ContentHeight);
            if (_Form.TryGetFocusRect(out Rect focus))
            {
                if (focus.Y < _Scroll)
                    _Scroll = focus.Y;
                else if (focus.Y + focus.Height > _Scroll + view)
                    _Scroll = focus.Y + focus.Height - view;
            }

            _Scroll = Math.Max(0, Math.Min(_Scroll, total - view));
            for (int y = 0; y < view && _Scroll + y < buffer.Height; y++)
            {
                for (int x = 0; x < width; x++)
                    surface.Set(x, 2 + y, buffer.Get(x, _Scroll + y));
            }
        }

        private void OpenWizard()
        {
            if (_App == null)
            {
                _Status = "The wizard needs the live app.";
                return;
            }

            TextField project = new TextField { Value = string.Empty };
            Checkbox import = new Checkbox("Import an existing repository", false);
            Form first = new Form();
            first.Add("Project name", project);
            first.Add(string.Empty, import);
            TextField url = new TextField { Value = "https://github.com/" };
            TextField description = new TextField { Placeholder = "optional description" };

            Wizard wizard = new Wizard(new[]
            {
                new WizardStep("Project", first) { Validate = () => project.Value.Trim().Length == 0 ? "Name the project first" : null },
                new WizardStep("Repository", url) { IsApplicable = () => import.Checked, Validate = () => url.Value.Length > "https://github.com/".Length ? null : "Enter the repository URL" },
                new WizardStep("Extras", description) { Optional = true },
            });
            WizardModal modal = new WizardModal("New project", wizard) { NaturalWidth = 56, NaturalHeight = 12 };
            modal.ApplyTheme(_App.Theme);
            _App.ShowAsync(modal).ContinueWith(
                t => _App.Post(() =>
                {
                    object? outcome = t.Status == TaskStatus.RanToCompletion ? t.Result : null;
                    _Status = outcome is WizardOutcome done && done == WizardOutcome.Finished
                        ? "Created " + project.Value + (import.Checked ? " from " + url.Value : string.Empty) + "."
                        : "Wizard cancelled.";
                }),
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }
    }
}

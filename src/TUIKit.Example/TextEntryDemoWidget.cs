namespace TUIKit.Example
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Widgets;

    // The "Text fields that log in" tour page (1.8, C1): a sign-in form whose user field arrives prefilled and
    // selected, and whose token field is masked with a Ctrl+R reveal key. Shell line-editing keys, a length
    // limit, validation, and Enter to submit are all live, and F5 to F9 switch each feature so its effect can be
    // seen. Tab and Up/Down move between the fields; a paste lands in the focused field.
    internal sealed class TextEntryDemoWidget : IWidget, IFocusContainer, IFocusAware, IMouseAware, IKeyHintSource, ITextEntry, ITextEntryKeys, IPasteTarget
    {
        private static readonly int[] _Limits = { 0, 8, 16 };
        private static readonly string[] _Modes = { "on submit", "on change", "off" };

        private readonly TextField _User = new TextField { Placeholder = "user name" };
        private readonly TextField _Token = new TextField { Placeholder = "access token", MaskChar = '*' };
        private int _Focus;
        private bool _Focused;
        private int _LimitIndex = 2;
        private int _Mode;
        private string _Status = "Enter submits. The first key replaces the remembered user.";

        internal TextEntryDemoWidget()
        {
            _User.Value = "ada.lovelace";
            _User.SelectAll();
            _User.UseLineEditingKeys = true;
            _Token.UseLineEditingKeys = true;
            _Token.RevealKey = KeyChord.Parse("ctrl+r");
            _User.ErrorStyle = CellStyle.Default.WithForeground(Color.FromPalette(1));
            _Token.ErrorStyle = CellStyle.Default.WithForeground(Color.FromPalette(1));
            _User.Validator = v => v.Trim().Length == 0 ? "User is required" : (v.Contains(" ") ? "No spaces in a user name" : null);
            _Token.Validator = v => v.Length < 6 ? "Token needs at least 6 characters" : null;
            _User.Submitted += (s, e) => Submit();
            _Token.Submitted += (s, e) => Submit();
            Apply();
        }

        public bool AcceptsText
        {
            get { return Current.AcceptsText; }
        }

        public KeyHint? LeaveHint
        {
            get { return null; }
        }

        private TextField Current
        {
            get { return _Focus == 0 ? _User : _Token; }
        }

        public IFocusable? FocusedLeaf
        {
            get { return Current; }
        }

        public bool MoveFocus(bool forward)
        {
            if (forward ? _Focus == 1 : _Focus == 0)
                return false;

            _Focus = forward ? 1 : 0;
            UpdateFocus();
            return true;
        }

        public void FocusEdge(bool first)
        {
            _Focus = first ? 0 : 1;
            UpdateFocus();
        }

        public bool ConsumesChord(KeyChord chord)
        {
            return Current.ConsumesChord(chord) || IsSetting(chord.Code);
        }

        public bool HandlePaste(string text)
        {
            return Current.HandlePaste(text);
        }

        public bool HandleKey(KeyEvent key)
        {
            switch (key.Code)
            {
                case KeyCode.F5:
                    _User.UseLineEditingKeys = !_User.UseLineEditingKeys;
                    _Token.UseLineEditingKeys = _User.UseLineEditingKeys;
                    _Status = "Line-editing keys " + OnOff(_User.UseLineEditingKeys) + ": Ctrl+A/E, Ctrl+U/K, Ctrl+W, Ctrl+Left/Right.";
                    return true;
                case KeyCode.F6:
                    _Token.RevealKey = _Token.RevealKey.HasValue ? (KeyChord?)null : KeyChord.Parse("ctrl+r");
                    _Token.IsRevealed = false;
                    _Status = "Reveal key " + (_Token.RevealKey.HasValue ? "Ctrl+R" : "off") + ".";
                    return true;
                case KeyCode.F7:
                    _LimitIndex = (_LimitIndex + 1) % _Limits.Length;
                    Apply();
                    _Status = "MaxLength " + (_Limits[_LimitIndex] == 0 ? "off" : _Limits[_LimitIndex] + " graphemes") + ". Pastes are cut to fit.";
                    return true;
                case KeyCode.F8:
                    _Mode = (_Mode + 1) % _Modes.Length;
                    Apply();
                    _Status = "Validation " + _Modes[_Mode] + ".";
                    return true;
                case KeyCode.F9:
                    _User.Value = "ada.lovelace";
                    _User.SelectAll();
                    _Token.Value = string.Empty;
                    _Focus = 0;
                    UpdateFocus();
                    _Status = "Reset: the remembered user is selected again.";
                    return true;
                case KeyCode.Tab:
                    if ((key.Modifiers & KeyModifiers.Shift) != 0 ? _Focus == 0 : _Focus == 1)
                        return false;
                    _Focus = 1 - _Focus;
                    UpdateFocus();
                    return true;
                case KeyCode.Up:
                case KeyCode.Down:
                    _Focus = key.Code == KeyCode.Up ? 0 : 1;
                    UpdateFocus();
                    return true;
            }

            return Current.HandleKey(key);
        }

        public IReadOnlyList<KeyHint>? GetKeyHints()
        {
            List<KeyHint> hints = new List<KeyHint>();
            IReadOnlyList<KeyHint>? own = Current.GetKeyHints();
            if (own != null)
                hints.AddRange(own);
            hints.Add(new KeyHint("F5-F9", "Settings"));
            return hints;
        }

        public void OnFocusChanged(bool focused)
        {
            _Focused = focused;
            UpdateFocus();
        }

        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            if (mouse.Y != 0 && mouse.Y != 1)
                return false;

            if (mouse.Kind == MouseEventKind.Press)
            {
                _Focus = mouse.Y;
                UpdateFocus();
            }

            return Current.HandleMouse(new MouseEvent(mouse.Kind, mouse.Button, mouse.X - 8, 0, mouse.Modifiers, mouse.ClickCount));
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
            if (width <= 10 || height <= 0)
                return;

            CellStyle label = CellStyle.Default.WithForeground(Color.FromPalette(8));
            CellStyle focusLabel = CellStyle.Default.WithForeground(Color.FromPalette(6)).WithAttribute(CellAttributes.Bold, true);
            CellStyle field = CellStyle.Default.WithBackground(Color.FromRgb(40, 40, 40));
            _User.NormalStyle = field;
            _Token.NormalStyle = field;

            surface.DrawText(0, 0, "User", _Focus == 0 ? focusLabel : label);
            _User.Render(new SurfaceView(surface, new Rect(8, 0, Math.Min(32, width - 8), 1)));
            if (height > 1)
            {
                surface.DrawText(0, 1, "Token", _Focus == 1 ? focusLabel : label);
                _Token.Render(new SurfaceView(surface, new Rect(8, 1, Math.Min(32, width - 8), 1)));
            }

            int y = 2;
            CellStyle error = CellStyle.Default.WithForeground(Color.FromPalette(1));
            string? message = _User.Error ?? _Token.Error;
            if (height > y)
                surface.DrawText(0, y, message != null ? "! " + message : string.Empty, error);

            y++;
            if (height > y)
                surface.DrawText(0, y, TUIKit.Unicode.TextFit.Ellipsize(_Status, width), CellStyle.Default.WithForeground(Color.FromPalette(2)));

            CellStyle muted = CellStyle.Default.WithForeground(Color.FromPalette(8));
            string[] settings =
            {
                "F5 line keys: " + OnOff(_User.UseLineEditingKeys) + "   F6 reveal: " + (_Token.RevealKey.HasValue ? "Ctrl+R" : "off"),
                "F7 max length: " + (_Limits[_LimitIndex] == 0 ? "off" : _Limits[_LimitIndex].ToString()) + "   F8 validate: " + _Modes[_Mode],
                "F9 reset   Tab/Up/Down field   paste goes to the field",
                "Selected: " + (_User.IsAllSelected ? "user (type to replace)" : "-") + "   Revealed: " + (_Token.IsRevealed ? "yes" : "no"),
            };
            for (int i = 0; i < settings.Length && y + 2 + i < height; i++)
                surface.DrawText(0, y + 2 + i, TUIKit.Unicode.TextFit.Ellipsize(settings[i], width), muted);
        }

        private void Submit()
        {
            if (_Mode == 2)
            {
                _Status = "Submitted " + _User.Value + " (validation off).";
                return;
            }

            bool userOk = _User.Validate();
            bool tokenOk = _Token.Validate();
            if (!userOk)
                _Focus = 0;
            else if (!tokenOk)
                _Focus = 1;
            UpdateFocus();
            _Status = userOk && tokenOk ? "Signed in as " + _User.Value + " with a " + _Token.Value.Length + "-character token." : "Fix the field shown in red.";
        }

        private void Apply()
        {
            _User.MaxLength = _Limits[_LimitIndex];
            _Token.MaxLength = _Limits[_LimitIndex] == 0 ? 0 : _Limits[_LimitIndex] * 2;
            _User.ValidateOnChange = _Mode == 1;
            _Token.ValidateOnChange = _Mode == 1;
            if (_Mode == 2)
            {
                _User.Validator = null;
                _Token.Validator = null;
                _User.Validate();
                _Token.Validate();
            }
            else
            {
                _User.Validator = v => v.Trim().Length == 0 ? "User is required" : (v.Contains(" ") ? "No spaces in a user name" : null);
                _Token.Validator = v => v.Length < 6 ? "Token needs at least 6 characters" : null;
            }
        }

        private void UpdateFocus()
        {
            _User.OnFocusChanged(_Focused && _Focus == 0);
            _Token.OnFocusChanged(_Focused && _Focus == 1);
        }

        private static bool IsSetting(KeyCode code)
        {
            return code == KeyCode.F5 || code == KeyCode.F6 || code == KeyCode.F7 || code == KeyCode.F8 || code == KeyCode.F9;
        }

        private static string OnOff(bool value)
        {
            return value ? "on" : "off";
        }
    }
}

namespace TUIKit.Widgets
{
    using System;
    using TUIKit;
    using TUIKit.Input;

    /// <summary>
    /// An interactive settings widget for viewing and changing key bindings, satisfying the common
    /// request for a user-editable keymap. It lists each command with its chord; Up/Down move the
    /// selection, Enter starts capture, and the next key press rebinds the selected command. A press
    /// that conflicts with another command's chord is rejected and reported in
    /// <see cref="LastError"/>; Escape cancels capture. Drop it into a modal or a settings pane.
    /// </summary>
    public sealed class KeyBindingEditor : IWidget, IFocusable
    {
        private readonly KeyBindingSet _Set;
        private int _Selected;
        private int _Top;
        private int _LastViewportHeight = 1;
        private bool _Capturing;

        /// <summary>
        /// Initializes a new instance of the <see cref="KeyBindingEditor"/> class.
        /// </summary>
        /// <param name="bindings">The binding set to edit. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="bindings"/> is null.</exception>
        public KeyBindingEditor(KeyBindingSet bindings)
        {
            _Set = bindings ?? throw new ArgumentNullException(nameof(bindings));
        }

        /// <summary>Gets whether the editor is waiting to capture a replacement chord.</summary>
        public bool IsCapturing
        {
            get { return _Capturing; }
        }

        /// <summary>Gets the index of the selected binding.</summary>
        public int SelectedIndex
        {
            get { return _Selected; }
        }

        /// <summary>Gets the last rebind error message, or null when the last action succeeded.</summary>
        public string? LastError { get; private set; }

        /// <inheritdoc/>
        public bool HandleKey(KeyEvent key)
        {
            if (_Capturing)
                return HandleCapture(key);

            switch (key.Code)
            {
                case KeyCode.Up:
                    _Selected = Math.Max(0, _Selected - 1);
                    return true;
                case KeyCode.Down:
                    _Selected = Math.Min(Math.Max(0, _Set.Bindings.Count - 1), _Selected + 1);
                    return true;
                case KeyCode.PageUp:
                    _Selected = Math.Max(0, _Selected - Math.Max(1, _LastViewportHeight));
                    return true;
                case KeyCode.PageDown:
                    _Selected = Math.Min(Math.Max(0, _Set.Bindings.Count - 1), _Selected + Math.Max(1, _LastViewportHeight));
                    return true;
                case KeyCode.Home:
                    _Selected = 0;
                    return true;
                case KeyCode.End:
                    _Selected = Math.Max(0, _Set.Bindings.Count - 1);
                    return true;
                case KeyCode.Enter:
                    if (_Set.Bindings.Count > 0)
                    {
                        _Capturing = true;
                        LastError = null;
                    }

                    return true;
                default:
                    return false;
            }
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            return new Size(available.Width, Math.Min(available.Height, _Set.Bindings.Count + 1));
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            int width = surface.Size.Width;
            int height = surface.Size.Height;
            if (width <= 0 || height <= 0)
                return;

            string header = _Capturing ? "Press a key to rebind, Esc to cancel" : "Enter to rebind";
            surface.DrawText(0, 0, Fit(header, width), CellStyle.Default.WithForeground(Color.FromPalette(6)).WithAttribute(CellAttributes.Bold, true));

            int listHeight = height - 1;
            if (listHeight <= 0)
                return;

            _LastViewportHeight = listHeight;

            if (_Selected < _Top)
                _Top = _Selected;
            else if (_Selected >= _Top + listHeight)
                _Top = _Selected - listHeight + 1;

            int chordColumn = Math.Max(1, width / 2);
            for (int row = 0; row < listHeight && _Top + row < _Set.Bindings.Count; row++)
            {
                int index = _Top + row;
                KeyBinding binding = _Set.Bindings[index];
                bool selected = index == _Selected;
                int y = row + 1;

                CellStyle style = selected
                    ? CellStyle.Default.WithForeground(Color.FromRgb(0, 0, 0)).WithBackground(Color.FromPalette(6))
                    : CellStyle.Default;
                if (selected)
                    surface.Fill(new Rect(0, y, width, 1), Cell.Blank(style));

                surface.DrawText(0, y, Fit(binding.Command, chordColumn - 1), style);
                string chord = selected && _Capturing ? "…" : binding.Chord.ToString();
                surface.DrawText(chordColumn, y, Fit(chord, width - chordColumn), style);
            }
        }

        private bool HandleCapture(KeyEvent key)
        {
            if (key.Code == KeyCode.Escape)
            {
                _Capturing = false;
                LastError = null;
                return true;
            }

            _Capturing = false;
            KeyBinding binding = _Set.Bindings[_Selected];
            KeyChord chord = KeyChord.FromKeyEvent(key);
            if (_Set.Rebind(binding.Command, chord))
                LastError = null;
            else
                LastError = "'" + chord.ToString() + "' is already bound to '" + _Set.CommandFor(chord) + "'.";

            return true;
        }

        private static string Fit(string text, int width)
        {
            if (text.Length <= width)
                return text;
            if (width <= 1)
                return text.Substring(0, Math.Max(0, width));

            return text.Substring(0, width - 1) + "…";
        }
    }
}

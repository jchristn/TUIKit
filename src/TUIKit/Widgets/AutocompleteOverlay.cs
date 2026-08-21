namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;

    /// <summary>
    /// A caret-anchored suggestion popup for a text input. Feed it the current input with
    /// <see cref="SetInput"/>; it asks an <see cref="ISuggestionProvider"/> for candidates and, when any
    /// exist, becomes visible. Up/Down move the highlight (wrapping), PageUp/PageDown move by a page
    /// (<see cref="MaxRows"/>), Home/End jump to the first and last suggestion, Tab or Enter accept the
    /// highlighted suggestion (raising <see cref="Accepted"/>), and Escape dismisses it. Draw it over existing
    /// content with <see cref="RenderAt"/>, which flips the list above the caret when there is no room
    /// below.
    /// </summary>
    public sealed class AutocompleteOverlay
    {
        private readonly ISuggestionProvider _Provider;
        private readonly List<string> _Suggestions = new List<string>();
        private int _Selected;

        /// <summary>
        /// Initializes a new instance of the <see cref="AutocompleteOverlay"/> class.
        /// </summary>
        /// <param name="provider">The suggestion provider. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="provider"/> is null.</exception>
        public AutocompleteOverlay(ISuggestionProvider provider)
        {
            _Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <summary>
        /// Raised when a suggestion is accepted, with the accepted text.
        /// </summary>
        public event Action<string>? Accepted;

        /// <summary>
        /// Raised when the overlay is dismissed with Escape.
        /// </summary>
        public event Action? Dismissed;

        private int _MaxRows = 6;

        /// <summary>
        /// Gets or sets the maximum number of suggestion rows shown at once. Must be greater than zero.
        /// Defaults to 6.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set to zero or less.</exception>
        public int MaxRows
        {
            get { return _MaxRows; }
            set
            {
                if (value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "MaxRows must be greater than zero.");
                _MaxRows = value;
            }
        }

        /// <summary>
        /// Gets or sets the style for a non-highlighted suggestion. Defaults to the terminal default on a dark panel.
        /// </summary>
        public CellStyle Style { get; set; } = CellStyle.Default.WithBackground(Color.FromPalette(0));

        /// <summary>
        /// Gets or sets the style for the highlighted suggestion. Defaults to black on cyan.
        /// </summary>
        public CellStyle HighlightStyle { get; set; } = CellStyle.Default
            .WithForeground(Color.FromRgb(0, 0, 0))
            .WithBackground(Color.FromPalette(6));

        /// <summary>
        /// Gets a value indicating whether the overlay currently has suggestions to show.
        /// </summary>
        public bool IsVisible
        {
            get { return _Suggestions.Count > 0; }
        }

        /// <summary>
        /// Gets the current suggestions. Never null.
        /// </summary>
        public IReadOnlyList<string> Suggestions
        {
            get { return _Suggestions; }
        }

        /// <summary>
        /// Gets the highlighted suggestion, or null when there are none.
        /// </summary>
        public string? SelectedSuggestion
        {
            get { return _Suggestions.Count == 0 ? null : _Suggestions[_Selected]; }
        }

        /// <summary>
        /// Recomputes the suggestions for the supplied input and resets the highlight to the first.
        /// </summary>
        /// <param name="input">The current input. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> is null.</exception>
        public void SetInput(string input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            _Suggestions.Clear();
            IReadOnlyList<string> suggestions = _Provider.Suggest(input);
            if (suggestions != null)
            {
                for (int i = 0; i < suggestions.Count; i++)
                    _Suggestions.Add(suggestions[i]);
            }

            _Selected = 0;
        }

        /// <summary>
        /// Hides the overlay by clearing its suggestions.
        /// </summary>
        public void Hide()
        {
            _Suggestions.Clear();
            _Selected = 0;
        }

        /// <summary>
        /// Handles a key while the overlay is visible: Up/Down move (wrapping), PageUp/PageDown move by a
        /// page, Home/End jump to the first and last suggestion, Tab/Enter accept, Escape dismisses.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns><c>true</c> when the overlay consumed the key; otherwise <c>false</c> (let the input handle it).</returns>
        public bool HandleKey(KeyEvent key)
        {
            if (!IsVisible)
                return false;

            switch (key.Code)
            {
                case KeyCode.Up:
                    _Selected = _Selected > 0 ? _Selected - 1 : _Suggestions.Count - 1;
                    return true;
                case KeyCode.Down:
                    _Selected = (_Selected + 1) % _Suggestions.Count;
                    return true;
                case KeyCode.PageUp:
                    _Selected = Math.Max(0, _Selected - _MaxRows);
                    return true;
                case KeyCode.PageDown:
                    _Selected = Math.Min(_Suggestions.Count - 1, _Selected + _MaxRows);
                    return true;
                case KeyCode.Home:
                    _Selected = 0;
                    return true;
                case KeyCode.End:
                    _Selected = _Suggestions.Count - 1;
                    return true;
                case KeyCode.Tab:
                case KeyCode.Enter:
                    string accepted = _Suggestions[_Selected];
                    Hide();
                    Accepted?.Invoke(accepted);
                    return true;
                case KeyCode.Escape:
                    Hide();
                    Dismissed?.Invoke();
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Draws the suggestion list over a surface, anchored to a caret position. The list appears below
        /// the caret when there is room and above it otherwise, and is clipped to the surface.
        /// </summary>
        /// <param name="surface">The target surface. Must not be null.</param>
        /// <param name="anchorX">The caret column.</param>
        /// <param name="anchorY">The caret row.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="surface"/> is null.</exception>
        public void RenderAt(ISurface surface, int anchorX, int anchorY)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));
            if (!IsVisible)
                return;

            int rows = Math.Min(_MaxRows, _Suggestions.Count);
            int width = 0;
            for (int i = 0; i < _Suggestions.Count; i++)
            {
                int w = TUIKit.Unicode.Graphemes.MeasureWidth(_Suggestions[i]);
                if (w > width)
                    width = w;
            }

            width = Math.Max(1, Math.Min(width + 1, surface.Size.Width - anchorX));
            if (width < 1)
                return;

            bool below = anchorY + 1 + rows <= surface.Size.Height;
            int top = below ? anchorY + 1 : Math.Max(0, anchorY - rows);

            int first = 0;
            if (_Selected >= rows)
                first = _Selected - rows + 1;

            for (int row = 0; row < rows; row++)
            {
                int index = first + row;
                if (index >= _Suggestions.Count)
                    break;

                bool selected = index == _Selected;
                CellStyle style = selected ? HighlightStyle : Style;
                int y = top + row;
                surface.Fill(new Rect(anchorX, y, width, 1), Cell.Blank(style));
                surface.DrawText(anchorX, y, Fit(_Suggestions[index], width), style);
            }
        }

        private static string Fit(string text, int maxWidth)
        {
            if (TUIKit.Unicode.Graphemes.MeasureWidth(text) <= maxWidth)
                return text;

            string clipped = text;
            while (clipped.Length > 0 && TUIKit.Unicode.Graphemes.MeasureWidth(clipped) > maxWidth)
                clipped = clipped.Substring(0, clipped.Length - 1);

            return clipped;
        }
    }
}

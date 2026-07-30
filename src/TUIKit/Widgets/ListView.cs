namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;

    /// <summary>
    /// A vertical list of selectable items with keyboard navigation and scrolling. The selected item
    /// is highlighted; the view scrolls to keep it visible.
    /// </summary>
    public sealed class ListView : IWidget, IFocusable, IFocusAware
    {
        private readonly List<string> _Items = new List<string>();
        private int _Selected;
        private int _Top;

        /// <summary>
        /// Gets or sets a value indicating whether the list is focused. When focused the selected item
        /// is drawn with a solid highlight bar; when not focused it is drawn as bold text only, so the
        /// selection stays visible without competing with the focused widget. Defaults to true so a
        /// standalone list looks the same as before. Set automatically by the host focus ring and
        /// <see cref="FocusManager"/> through <see cref="IFocusAware"/>.
        /// </summary>
        public bool IsFocused { get; set; } = true;

        /// <summary>
        /// Updates the focused state so the selection highlight reflects focus on the next frame. Part
        /// of <see cref="IFocusAware"/>.
        /// </summary>
        /// <param name="focused"><c>true</c> when the list has gained focus; otherwise <c>false</c>.</param>
        public void OnFocusChanged(bool focused)
        {
            IsFocused = focused;
        }

        /// <summary>
        /// Gets the items. Never null.
        /// </summary>
        public IReadOnlyList<string> Items
        {
            get { return _Items; }
        }

        /// <summary>
        /// Gets the zero-based index of the selected item, or -1 when the list is empty.
        /// </summary>
        public int SelectedIndex
        {
            get { return _Items.Count == 0 ? -1 : _Selected; }
        }

        /// <summary>
        /// Gets the selected item text, or null when the list is empty.
        /// </summary>
        public string? SelectedItem
        {
            get { return _Items.Count == 0 ? null : _Items[_Selected]; }
        }

        /// <summary>
        /// Gets or sets the highlight color for the selected item. Defaults to palette cyan.
        /// </summary>
        public Color HighlightColor { get; set; } = Color.FromPalette(6);

        /// <summary>
        /// Replaces the list items and resets the selection to the first item.
        /// </summary>
        /// <param name="items">The items. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="items"/> is null.</exception>
        public void SetItems(IEnumerable<string> items)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            _Items.Clear();
            _Items.AddRange(items);
            _Selected = 0;
            _Top = 0;
        }

        /// <summary>
        /// Moves the selection down by one item.
        /// </summary>
        public void SelectNext()
        {
            if (_Items.Count == 0)
                return;

            _Selected = Math.Min(_Items.Count - 1, _Selected + 1);
        }

        /// <summary>
        /// Moves the selection up by one item.
        /// </summary>
        public void SelectPrevious()
        {
            if (_Items.Count == 0)
                return;

            _Selected = Math.Max(0, _Selected - 1);
        }

        /// <summary>
        /// Handles Up/Down navigation keys.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns><c>true</c> when the key was consumed; otherwise <c>false</c>.</returns>
        public bool HandleKey(KeyEvent key)
        {
            if (key.Code == KeyCode.Up)
            {
                SelectPrevious();
                return true;
            }

            if (key.Code == KeyCode.Down)
            {
                SelectNext();
                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            return new Size(available.Width, Math.Min(available.Height, _Items.Count));
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            int height = surface.Size.Height;
            int width = surface.Size.Width;
            if (height <= 0 || width <= 0)
                return;

            if (_Selected < _Top)
                _Top = _Selected;
            else if (_Selected >= _Top + height)
                _Top = _Selected - height + 1;

            surface.Fill(new Rect(0, 0, width, height), Cell.Blank(CellStyle.Default));

            for (int row = 0; row < height && _Top + row < _Items.Count; row++)
            {
                int index = _Top + row;
                bool selected = index == _Selected;
                CellStyle style;
                if (selected && IsFocused)
                {
                    style = CellStyle.Default.WithForeground(Color.FromRgb(0, 0, 0)).WithBackground(HighlightColor);
                    surface.Fill(new Rect(0, row, width, 1), Cell.Blank(style));
                }
                else if (selected)
                {
                    style = CellStyle.Default.WithForeground(HighlightColor).WithAttribute(CellAttributes.Bold, true);
                }
                else
                {
                    style = CellStyle.Default;
                }

                surface.DrawText(0, row, _Items[index], style);
            }
        }
    }
}

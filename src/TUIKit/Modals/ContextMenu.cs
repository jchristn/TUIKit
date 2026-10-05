namespace TUIKit.Modals
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Theming;
    using TUIKit.Unicode;
    using TUIKit.Widgets;

    /// <summary>
    /// A popup menu shown at a screen position (for example where the user right-clicked, or at the
    /// selected row), clamped to the screen. Up/Down move between enabled items, a letter jumps to the
    /// next item starting with it, Enter or a click picks, and Escape or a click outside closes. Picking
    /// runs the item's action and completes with the item index; closing completes with -1. An item whose
    /// label is <see cref="Separator"/> draws a divider line and cannot be picked.
    /// </summary>
    /// <remarks>Not thread-safe: use it from the UI loop.</remarks>
    public sealed class ContextMenu : Modal, IThemeable
    {
        /// <summary>
        /// The label that marks a separator item.
        /// </summary>
        public const string Separator = "-";

        private readonly List<MenuItem> _Items = new List<MenuItem>();
        private readonly int _AnchorX;
        private readonly int _AnchorY;
        private int _Highlight = -1;
        private Rect _Box;

        /// <summary>
        /// Gets or sets the menu style. Defaults to <see cref="CellStyle.Default"/>.
        /// </summary>
        public CellStyle MenuStyle { get; set; } = CellStyle.Default;

        /// <summary>
        /// Gets or sets the border style. Defaults to cyan (palette 6).
        /// </summary>
        public CellStyle BorderStyleColor { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(6));

        /// <summary>
        /// Gets or sets the highlighted item style. Defaults to black on cyan (palette 6).
        /// </summary>
        public CellStyle HighlightStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromRgb(0, 0, 0)).WithBackground(Color.FromPalette(6));

        /// <summary>
        /// Gets or sets the disabled item style. Defaults to dim text.
        /// </summary>
        public CellStyle DisabledStyle { get; set; } = CellStyle.Default.WithAttribute(CellAttributes.Dim, true);

        /// <summary>
        /// Gets the items. Never null.
        /// </summary>
        public IReadOnlyList<MenuItem> Items
        {
            get { return _Items; }
        }

        /// <summary>
        /// Gets the highlighted item index, or -1.
        /// </summary>
        public int HighlightedIndex
        {
            get { return _Highlight; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ContextMenu"/> class.
        /// </summary>
        /// <param name="items">The items. Must not be null, empty, or contain null.</param>
        /// <param name="anchorX">The screen column of the menu's top-left corner.</param>
        /// <param name="anchorY">The screen row of the menu's top-left corner.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="items"/> or an item is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="items"/> is empty.</exception>
        public ContextMenu(IEnumerable<MenuItem> items, int anchorX, int anchorY)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            foreach (MenuItem item in items)
                _Items.Add(item ?? throw new ArgumentNullException(nameof(items), "Items must not contain null."));

            if (_Items.Count == 0)
                throw new ArgumentException("At least one item is required.", nameof(items));

            _AnchorX = Math.Max(0, anchorX);
            _AnchorY = Math.Max(0, anchorY);
            _Highlight = NextSelectable(-1, 1);
        }

        /// <summary>
        /// Applies a theme.
        /// </summary>
        /// <param name="theme">The theme. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
        public void ApplyTheme(Theme theme)
        {
            if (theme == null)
                throw new ArgumentNullException(nameof(theme));

            MenuStyle = theme.Text;
            BorderStyleColor = theme.Border;
            HighlightStyle = theme.Selection;
            DisabledStyle = theme.Disabled;
        }

        /// <inheritdoc/>
        public override bool HandleKey(KeyEvent key)
        {
            switch (key.Code)
            {
                case KeyCode.Escape:
                    RequestClose(-1);
                    return true;
                case KeyCode.Up:
                    Move(-1);
                    return true;
                case KeyCode.Down:
                case KeyCode.Tab:
                    Move(1);
                    return true;
                case KeyCode.Home:
                    _Highlight = NextSelectable(-1, 1);
                    return true;
                case KeyCode.End:
                    _Highlight = NextSelectable(_Items.Count, -1);
                    return true;
                case KeyCode.Enter:
                    Pick(_Highlight);
                    return true;
                case KeyCode.Character:
                    string prefix = char.ConvertFromUtf32(key.Rune);
                    for (int offset = 1; offset <= _Items.Count; offset++)
                    {
                        int index = (Math.Max(0, _Highlight) + offset) % _Items.Count;
                        if (Selectable(index) && _Items[index].Label.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            _Highlight = index;
                            break;
                        }
                    }

                    return true;
                default:
                    return true;
            }
        }

        /// <inheritdoc/>
        public override bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            bool inside = _Box.Contains(new Point(mouse.X, mouse.Y));
            int index = inside ? mouse.Y - _Box.Y - 1 : -1;
            if (mouse.Kind == MouseEventKind.Move || mouse.Kind == MouseEventKind.Enter)
            {
                if (index >= 0 && index < _Items.Count && Selectable(index))
                    _Highlight = index;
                return true;
            }

            if (mouse.Kind == MouseEventKind.Wheel)
            {
                Move(mouse.Button == MouseButton.WheelUp ? -1 : 1);
                return true;
            }

            if (mouse.Kind != MouseEventKind.Press)
                return true;

            if (!inside)
            {
                RequestClose(-1);
                return true;
            }

            if (index >= 0 && index < _Items.Count)
                Pick(index);

            return true;
        }

        /// <inheritdoc/>
        public override void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            int longest = 1;
            for (int i = 0; i < _Items.Count; i++)
                longest = Math.Max(longest, TextFit.Width(_Items[i].Label));

            int width = Math.Min(surface.Size.Width, longest + 4);
            int height = Math.Min(surface.Size.Height, _Items.Count + 2);
            if (width < 3 || height < 3)
            {
                _Box = default;
                return;
            }

            int x = Math.Min(_AnchorX, surface.Size.Width - width);
            int y = Math.Min(_AnchorY, surface.Size.Height - height);
            _Box = new Rect(x, y, width, height);
            surface.Fill(_Box, Cell.Blank(MenuStyle));
            surface.DrawBox(_Box, BorderStyleColor, BorderStyle.Line);

            for (int i = 0; i < _Items.Count && i < height - 2; i++)
            {
                MenuItem item = _Items[i];
                int row = y + 1 + i;
                if (item.Label == Separator)
                {
                    surface.DrawHorizontalLine(x + 1, row, width - 2, "\u2500", BorderStyleColor);
                    continue;
                }

                CellStyle style = !item.Enabled ? DisabledStyle.Over(MenuStyle) : (i == _Highlight ? HighlightStyle : MenuStyle);
                if (i == _Highlight && item.Enabled)
                    surface.Fill(new Rect(x + 1, row, width - 2, 1), Cell.Blank(style));
                surface.DrawText(x + 2, row, TextFit.Ellipsize(item.Label, width - 4), style);
            }
        }

        private bool Selectable(int index)
        {
            return index >= 0 && index < _Items.Count && _Items[index].Enabled && _Items[index].Label != Separator;
        }

        private int NextSelectable(int from, int direction)
        {
            for (int i = from + direction; i >= 0 && i < _Items.Count; i += direction)
            {
                if (Selectable(i))
                    return i;
            }

            return -1;
        }

        private void Move(int direction)
        {
            int next = NextSelectable(_Highlight, direction);
            if (next >= 0)
                _Highlight = next;
        }

        private void Pick(int index)
        {
            if (!Selectable(index))
                return;

            Close(index);
            _Items[index].Action?.Invoke();
        }
    }
}

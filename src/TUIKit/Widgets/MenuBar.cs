namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;

    /// <summary>
    /// A horizontal menu bar with drop-down menus. Left/Right move between menus; Down or Enter opens
    /// the active menu; Up/Down move the highlight; Home/End jump to the first and last item; Enter
    /// activates the highlighted item and runs its action; Escape closes. While a menu is open,
    /// Left/Right switch menus and keep the drop-down open, matching common desktop behavior. The bar
    /// traps navigation keys only while it has focus.
    /// </summary>
    public sealed class MenuBar : IWidget, IFocusable, IMouseAware
    {
        private readonly List<Menu> _Menus = new List<Menu>();
        private int _Active;
        private int _Highlight;
        private bool _Open;
        private int _HoverTitle = -1;
        private Rect _LastDropdownRect;

        /// <summary>
        /// Gets or sets the style of a non-active menu title under the pointer while hover tracking
        /// is on. The active title keeps its highlight when hovered. Defaults to underlined default
        /// text.
        /// </summary>
        public CellStyle HoverStyle { get; set; } = CellStyle.Default.WithAttributes(CellAttributes.Underline);

        /// <summary>
        /// Gets or sets the style of the menu-bar strip behind the titles. Defaults to a black
        /// (palette 0) background.
        /// </summary>
        public CellStyle BarStyle { get; set; } = CellStyle.Default.WithBackground(Color.FromPalette(0));

        /// <summary>
        /// Gets or sets the style of the active menu title and the highlighted drop-down item.
        /// Defaults to black text on a cyan (palette 6) background.
        /// </summary>
        public CellStyle ActiveStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromRgb(0, 0, 0)).WithBackground(Color.FromPalette(6));

        /// <summary>
        /// Gets or sets the style of inactive menu titles and enabled, non-highlighted drop-down items.
        /// On the bar it is composed over <see cref="BarStyle"/>. Defaults to <see cref="CellStyle.Default"/>.
        /// </summary>
        public CellStyle ItemStyle { get; set; } = CellStyle.Default;

        /// <summary>
        /// Gets or sets the style of disabled drop-down items. It is composed over
        /// <see cref="DropdownStyle"/>. Defaults to dimmed default text.
        /// </summary>
        public CellStyle DisabledStyle { get; set; } = CellStyle.Default.WithAttribute(CellAttributes.Dim, true);

        /// <summary>
        /// Gets or sets the style of the open drop-down panel background. Defaults to a black
        /// (palette 0) background.
        /// </summary>
        public CellStyle DropdownStyle { get; set; } = CellStyle.Default.WithBackground(Color.FromPalette(0));

        /// <summary>
        /// Gets or sets the style of the open drop-down border. Defaults to a grey (palette 8)
        /// foreground.
        /// </summary>
        public CellStyle DropdownBorderStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(8));

        /// <summary>Gets whether a drop-down is currently open.</summary>
        public bool IsOpen
        {
            get { return _Open; }
        }

        /// <summary>Gets the index of the active (highlighted) menu in the bar.</summary>
        public int ActiveMenu
        {
            get { return _Active; }
        }

        /// <summary>Gets the highlighted item index within the open menu.</summary>
        public int HighlightedItem
        {
            get { return _Highlight; }
        }

        /// <summary>Gets the number of menus.</summary>
        public int Count
        {
            get { return _Menus.Count; }
        }

        /// <summary>
        /// Creates and adds a menu with the given title.
        /// </summary>
        /// <param name="title">The menu title. Must not be null.</param>
        /// <returns>The created menu, for adding items.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="title"/> is null.</exception>
        public Menu AddMenu(string title)
        {
            Menu menu = new Menu(title);
            _Menus.Add(menu);
            return menu;
        }

        /// <summary>
        /// Adds a pre-built menu.
        /// </summary>
        /// <param name="menu">The menu. Must not be null.</param>
        /// <returns>This menu bar, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="menu"/> is null.</exception>
        public MenuBar Add(Menu menu)
        {
            if (menu == null)
                throw new ArgumentNullException(nameof(menu));

            _Menus.Add(menu);
            return this;
        }

        /// <summary>
        /// Closes any open drop-down.
        /// </summary>
        public void Close()
        {
            _Open = false;
        }

        /// <inheritdoc/>
        public bool HandleKey(KeyEvent key)
        {
            if (_Menus.Count == 0)
                return false;

            if (!_Open)
                return HandleClosed(key);

            return HandleOpen(key);
        }

        /// <summary>
        /// Routes mouse interaction: a left press on a title opens (or toggles) that menu, a press on
        /// a drop-down item activates it, and a press elsewhere closes an open drop-down. Pointer
        /// motion hovers titles (rendered with <see cref="HoverStyle"/>) and moves the drop-down
        /// highlight, matching desktop menu behavior. Enter/Move/Leave events are never consumed.
        /// </summary>
        /// <param name="mouse">The mouse event in widget-local coordinates. Must not be null.</param>
        /// <returns><c>true</c> when a press changed menu state; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mouse"/> is null.</exception>
        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            if (_Menus.Count == 0)
                return false;

            switch (mouse.Kind)
            {
                case MouseEventKind.Press:
                    return mouse.Button == MouseButton.Left && HandlePress(mouse.X, mouse.Y);
                case MouseEventKind.Enter:
                case MouseEventKind.Move:
                    UpdateHoverState(mouse.X, mouse.Y);
                    return false;
                case MouseEventKind.Leave:
                    _HoverTitle = -1;
                    return false;
                default:
                    return false;
            }
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            return available;
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            int width = surface.Size.Width;
            int height = surface.Size.Height;
            if (width <= 0 || height <= 0 || _Menus.Count == 0)
                return;

            surface.Fill(new Rect(0, 0, width, 1), Cell.Blank(BarStyle));

            int x = 0;
            int activeX = 0;
            for (int i = 0; i < _Menus.Count; i++)
            {
                string title = " " + _Menus[i].Title + " ";
                bool active = i == _Active;
                CellStyle style;
                if (active)
                    style = ActiveStyle;
                else if (i == _HoverTitle)
                    style = HoverStyle.Over(BarStyle);
                else
                    style = ItemStyle.Over(BarStyle);
                surface.DrawText(x, 0, title, style);
                if (active)
                    activeX = x;

                x += title.Length;
            }

            if (_Open && height > 1)
                RenderDropdown(surface, activeX, width, height);
        }

        private bool HandleClosed(KeyEvent key)
        {
            switch (key.Code)
            {
                case KeyCode.Left:
                    _Active = (_Active - 1 + _Menus.Count) % _Menus.Count;
                    return true;
                case KeyCode.Right:
                    _Active = (_Active + 1) % _Menus.Count;
                    return true;
                case KeyCode.Down:
                case KeyCode.Enter:
                    _Open = true;
                    _Highlight = 0;
                    return true;
                default:
                    return false;
            }
        }

        private bool HandleOpen(KeyEvent key)
        {
            IReadOnlyList<MenuItem> items = _Menus[_Active].Items;
            switch (key.Code)
            {
                case KeyCode.Escape:
                    _Open = false;
                    return true;
                case KeyCode.Up:
                    if (items.Count > 0)
                        _Highlight = (_Highlight - 1 + items.Count) % items.Count;
                    return true;
                case KeyCode.Down:
                    if (items.Count > 0)
                        _Highlight = (_Highlight + 1) % items.Count;
                    return true;
                case KeyCode.Home:
                    _Highlight = 0;
                    return true;
                case KeyCode.End:
                    if (items.Count > 0)
                        _Highlight = items.Count - 1;
                    return true;
                case KeyCode.Left:
                    _Active = (_Active - 1 + _Menus.Count) % _Menus.Count;
                    _Highlight = 0;
                    return true;
                case KeyCode.Right:
                    _Active = (_Active + 1) % _Menus.Count;
                    _Highlight = 0;
                    return true;
                case KeyCode.Enter:
                    Activate(items);
                    return true;
                default:
                    return false;
            }
        }

        private bool HandlePress(int x, int y)
        {
            if (y == 0)
            {
                int title = TitleIndexAt(x);
                if (title >= 0)
                {
                    if (_Open && title == _Active)
                    {
                        _Open = false;
                    }
                    else
                    {
                        _Active = title;
                        _Open = true;
                        _Highlight = 0;
                    }

                    return true;
                }

                if (_Open)
                {
                    _Open = false;
                    return true;
                }

                return false;
            }

            if (_Open)
            {
                int item = DropdownItemAt(x, y);
                if (item >= 0)
                {
                    _Highlight = item;
                    Activate(_Menus[_Active].Items);
                    return true;
                }

                // A press outside the open drop-down dismisses it, like desktop menus.
                _Open = false;
                return true;
            }

            return false;
        }

        private void UpdateHoverState(int x, int y)
        {
            _HoverTitle = y == 0 ? TitleIndexAt(x) : -1;

            if (_Open)
            {
                int item = DropdownItemAt(x, y);
                if (item >= 0 && _Menus[_Active].Items[item].Enabled)
                    _Highlight = item;
            }
        }

        private int TitleIndexAt(int x)
        {
            if (x < 0)
                return -1;

            // Mirrors the render pass: each title occupies " title " with no gap between titles.
            int cursor = 0;
            for (int i = 0; i < _Menus.Count; i++)
            {
                int titleWidth = _Menus[i].Title.Length + 2;
                if (x >= cursor && x < cursor + titleWidth)
                    return i;

                cursor += titleWidth;
            }

            return -1;
        }

        private int DropdownItemAt(int x, int y)
        {
            Rect box = _LastDropdownRect;
            if (box.Width < 3 || box.Height < 3)
                return -1;
            if (x <= box.X || x >= box.X + box.Width - 1)
                return -1;

            // Items start on the row below the box's top border (box.Y + 1).
            int item = y - (box.Y + 1);
            IReadOnlyList<MenuItem> items = _Menus[_Active].Items;
            if (item < 0 || item >= items.Count || item >= box.Height - 2)
                return -1;

            return item;
        }

        private void Activate(IReadOnlyList<MenuItem> items)
        {
            if (_Highlight >= 0 && _Highlight < items.Count && items[_Highlight].Enabled)
            {
                _Open = false;
                items[_Highlight].Action?.Invoke();
            }
        }

        private void RenderDropdown(ISurface surface, int menuX, int width, int height)
        {
            IReadOnlyList<MenuItem> items = _Menus[_Active].Items;
            if (items.Count == 0)
                return;

            int longest = 0;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Label.Length > longest)
                    longest = items[i].Label.Length;
            }

            int boxWidth = Math.Min(width - menuX, longest + 4);
            int boxHeight = Math.Min(height - 1, items.Count + 2);
            if (boxWidth < 3 || boxHeight < 3)
                return;

            Rect box = new Rect(menuX, 1, boxWidth, boxHeight);
            _LastDropdownRect = box;
            surface.Fill(box, Cell.Blank(DropdownStyle));
            surface.DrawBox(box, DropdownBorderStyle, BorderStyle.Line);

            for (int i = 0; i < items.Count && i < boxHeight - 2; i++)
            {
                bool highlighted = i == _Highlight;
                CellStyle style;
                if (!items[i].Enabled)
                    style = DisabledStyle.Over(DropdownStyle);
                else if (highlighted)
                    style = ActiveStyle;
                else
                    style = ItemStyle.Over(DropdownStyle);

                string label = items[i].Label;
                if (label.Length > boxWidth - 2)
                    label = label.Substring(0, Math.Max(0, boxWidth - 2));

                if (highlighted && items[i].Enabled)
                    surface.Fill(new Rect(menuX + 1, 2 + i, boxWidth - 2, 1), Cell.Blank(style));

                surface.DrawText(menuX + 1, 2 + i, " " + label, style);
            }
        }
    }
}

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
    public sealed class MenuBar : IWidget, IFocusable
    {
        private readonly List<Menu> _Menus = new List<Menu>();
        private int _Active;
        private int _Highlight;
        private bool _Open;

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

            surface.Fill(new Rect(0, 0, width, 1), Cell.Blank(CellStyle.Default.WithBackground(Color.FromPalette(0))));

            int x = 0;
            int activeX = 0;
            for (int i = 0; i < _Menus.Count; i++)
            {
                string title = " " + _Menus[i].Title + " ";
                bool active = i == _Active;
                CellStyle style = active
                    ? CellStyle.Default.WithForeground(Color.FromRgb(0, 0, 0)).WithBackground(Color.FromPalette(6))
                    : CellStyle.Default;
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
            surface.Fill(box, Cell.Blank(CellStyle.Default.WithBackground(Color.FromPalette(0))));
            surface.DrawBox(box, CellStyle.Default.WithForeground(Color.FromPalette(8)), BorderStyle.Line);

            for (int i = 0; i < items.Count && i < boxHeight - 2; i++)
            {
                bool highlighted = i == _Highlight;
                CellStyle style;
                if (!items[i].Enabled)
                    style = CellStyle.Default.WithAttribute(CellAttributes.Dim, true);
                else if (highlighted)
                    style = CellStyle.Default.WithForeground(Color.FromRgb(0, 0, 0)).WithBackground(Color.FromPalette(6));
                else
                    style = CellStyle.Default;

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

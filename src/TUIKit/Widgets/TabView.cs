namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;

    /// <summary>
    /// A tabbed container that shows one of several child widgets with a tab strip along the top.
    /// Tab or Right activates the next tab; Left activates the previous. Clicking a tab header
    /// activates it, and the header under the pointer renders with <see cref="HoverStyle"/> while
    /// hover tracking is on.
    /// </summary>
    public sealed class TabView : IWidget, IFocusable, IMouseAware
    {
        private readonly List<string> _Names = new List<string>();
        private readonly List<IWidget> _Widgets = new List<IWidget>();
        private int _Active;
        private int _HoverTab = -1;

        /// <summary>
        /// Gets or sets the style of the active tab. Defaults to reversed cyan.
        /// </summary>
        public CellStyle ActiveStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromRgb(0, 0, 0)).WithBackground(Color.FromPalette(6));

        /// <summary>
        /// Gets or sets the style of inactive tabs. Defaults to muted.
        /// </summary>
        public CellStyle InactiveStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(8));

        /// <summary>
        /// Gets or sets the style of an inactive tab header under the pointer. The active tab keeps
        /// <see cref="ActiveStyle"/> while hovered. Defaults to underlined default text.
        /// </summary>
        public CellStyle HoverStyle { get; set; } = CellStyle.Default.WithAttributes(CellAttributes.Underline);

        /// <summary>
        /// Gets the zero-based index of the active tab, or -1 when there are no tabs.
        /// </summary>
        public int ActiveIndex
        {
            get { return _Widgets.Count == 0 ? -1 : _Active; }
        }

        /// <summary>
        /// Gets the number of tabs.
        /// </summary>
        public int Count
        {
            get { return _Widgets.Count; }
        }

        /// <summary>
        /// Adds a tab.
        /// </summary>
        /// <param name="name">The tab label. Must not be null.</param>
        /// <param name="widget">The tab content. Must not be null.</param>
        /// <returns>This tab view, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public TabView Add(string name, IWidget widget)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (widget == null)
                throw new ArgumentNullException(nameof(widget));

            _Names.Add(name);
            _Widgets.Add(widget);
            return this;
        }

        /// <summary>
        /// Activates a tab by index.
        /// </summary>
        /// <param name="index">The zero-based tab index.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is out of range.</exception>
        public void Activate(int index)
        {
            if (index < 0 || index >= _Widgets.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            _Active = index;
        }

        /// <summary>
        /// Switches tabs with Tab/Right (next) and Left (previous).
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns><c>true</c> when the key switched tabs; otherwise <c>false</c>.</returns>
        public bool HandleKey(KeyEvent key)
        {
            if (_Widgets.Count == 0)
                return false;

            if (key.Code == KeyCode.Tab || key.Code == KeyCode.Right)
            {
                _Active = (_Active + 1) % _Widgets.Count;
                return true;
            }

            if (key.Code == KeyCode.Left)
            {
                _Active = (_Active - 1 + _Widgets.Count) % _Widgets.Count;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Activates the tab header under a left press and tracks the hovered header for
        /// <see cref="HoverStyle"/> rendering. Enter/Move/Leave events are observed but never
        /// consumed.
        /// </summary>
        /// <param name="mouse">The mouse event in widget-local coordinates. Must not be null.</param>
        /// <returns><c>true</c> when a press activated a tab; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mouse"/> is null.</exception>
        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            switch (mouse.Kind)
            {
                case MouseEventKind.Press:
                    if (mouse.Button == MouseButton.Left)
                    {
                        int pressed = TabIndexAt(mouse.X, mouse.Y);
                        if (pressed >= 0)
                        {
                            _Active = pressed;
                            return true;
                        }
                    }

                    return false;
                case MouseEventKind.Enter:
                case MouseEventKind.Move:
                    _HoverTab = TabIndexAt(mouse.X, mouse.Y);
                    return false;
                case MouseEventKind.Leave:
                    _HoverTab = -1;
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
            if (width <= 0 || height <= 0 || _Widgets.Count == 0)
                return;

            int cursor = 0;
            for (int i = 0; i < _Names.Count && cursor < width; i++)
            {
                string label = " " + _Names[i] + " ";
                CellStyle style = i == _Active ? ActiveStyle : (i == _HoverTab ? HoverStyle : InactiveStyle);
                cursor += surface.DrawText(cursor, 0, label, style);
                cursor += surface.DrawText(cursor, 0, " ", InactiveStyle);
            }

            if (height > 1 && surface is BufferSurface buffer)
                _Widgets[_Active].Render(buffer.CreateView(new Rect(0, 1, width, height - 1)));
        }

        private int TabIndexAt(int x, int y)
        {
            if (y != 0 || x < 0)
                return -1;

            // Mirrors the render pass: each header occupies " name " followed by a one-cell gap.
            int cursor = 0;
            for (int i = 0; i < _Names.Count; i++)
            {
                int headerWidth = _Names[i].Length + 2;
                if (x >= cursor && x < cursor + headerWidth)
                    return i;

                cursor += headerWidth + 1;
            }

            return -1;
        }
    }
}

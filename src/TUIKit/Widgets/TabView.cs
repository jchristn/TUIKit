namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;

    /// <summary>
    /// A tabbed container that shows one of several child widgets with a tab strip along the top.
    /// Tab or Right activates the next tab; Left activates the previous.
    /// </summary>
    public sealed class TabView : IWidget, IFocusable
    {
        private readonly List<string> _Names = new List<string>();
        private readonly List<IWidget> _Widgets = new List<IWidget>();
        private int _Active;

        /// <summary>
        /// Gets or sets the style of the active tab. Defaults to reversed cyan.
        /// </summary>
        public CellStyle ActiveStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromRgb(0, 0, 0)).WithBackground(Color.FromPalette(6));

        /// <summary>
        /// Gets or sets the style of inactive tabs. Defaults to muted.
        /// </summary>
        public CellStyle InactiveStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(8));

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
                cursor += surface.DrawText(cursor, 0, label, i == _Active ? ActiveStyle : InactiveStyle);
                cursor += surface.DrawText(cursor, 0, " ", InactiveStyle);
            }

            if (height > 1 && surface is BufferSurface buffer)
                _Widgets[_Active].Render(buffer.CreateView(new Rect(0, 1, width, height - 1)));
        }
    }
}

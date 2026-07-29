namespace TUIKit.Widgets
{
    using System;
    using System.Globalization;
    using TUIKit;
    using TUIKit.Input;

    /// <summary>
    /// An interactive RGB color picker. Three channel rows (red, green, blue) each show a slider and
    /// the numeric value; a preview swatch renders the composed color and its hex string. Up/Down
    /// select a channel, Left/Right nudge it, PageUp/PageDown jump by sixteen, and Home/End snap to
    /// the extremes. Read <see cref="Value"/> for the chosen color.
    /// </summary>
    public sealed class ColorPicker : IWidget, IFocusable
    {
        private readonly int[] _Channels = new int[3];
        private int _Active;

        /// <summary>
        /// Initializes a new instance of the <see cref="ColorPicker"/> class.
        /// </summary>
        /// <param name="initial">The initial color.</param>
        public ColorPicker(Color initial)
        {
            _Channels[0] = initial.R;
            _Channels[1] = initial.G;
            _Channels[2] = initial.B;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ColorPicker"/> class starting from black.
        /// </summary>
        public ColorPicker()
            : this(Color.FromRgb(0, 0, 0))
        {
        }

        /// <summary>
        /// Gets the currently composed color.
        /// </summary>
        public Color Value
        {
            get { return Color.FromRgb((byte)_Channels[0], (byte)_Channels[1], (byte)_Channels[2]); }
        }

        /// <summary>
        /// Gets the zero-based index of the active channel (0 = red, 1 = green, 2 = blue).
        /// </summary>
        public int ActiveChannel
        {
            get { return _Active; }
        }

        /// <summary>
        /// Gets the value of the active channel (0–255).
        /// </summary>
        public int ActiveValue
        {
            get { return _Channels[_Active]; }
        }

        /// <inheritdoc/>
        public bool HandleKey(KeyEvent key)
        {
            switch (key.Code)
            {
                case KeyCode.Up:
                    _Active = (_Active + 2) % 3;
                    return true;
                case KeyCode.Down:
                    _Active = (_Active + 1) % 3;
                    return true;
                case KeyCode.Left:
                    Nudge(-1);
                    return true;
                case KeyCode.Right:
                    Nudge(1);
                    return true;
                case KeyCode.PageUp:
                    Nudge(16);
                    return true;
                case KeyCode.PageDown:
                    Nudge(-16);
                    return true;
                case KeyCode.Home:
                    _Channels[_Active] = 0;
                    return true;
                case KeyCode.End:
                    _Channels[_Active] = 255;
                    return true;
                default:
                    return false;
            }
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            return new Size(available.Width, Math.Min(available.Height, 4));
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

            string[] labels = { "R", "G", "B" };
            byte[] hues = { 1, 2, 4 };

            for (int c = 0; c < 3 && c < height; c++)
            {
                bool active = c == _Active;
                CellStyle labelStyle = active
                    ? CellStyle.Default.WithForeground(Color.FromPalette(6)).WithAttribute(CellAttributes.Bold, true)
                    : CellStyle.Default;
                surface.DrawText(0, c, (active ? "▸" : " ") + labels[c], labelStyle);

                int barStart = 3;
                int valueWidth = 4;
                int barWidth = width - barStart - valueWidth - 1;
                if (barWidth > 0)
                {
                    int filled = (int)Math.Round(_Channels[c] / 255.0 * barWidth);
                    CellStyle barStyle = CellStyle.Default.WithForeground(Color.FromPalette(hues[c]));
                    for (int x = 0; x < barWidth; x++)
                        surface.DrawText(barStart + x, c, x < filled ? "█" : "░", barStyle);
                }

                surface.DrawText(width - valueWidth, c, _Channels[c].ToString(CultureInfo.InvariantCulture).PadLeft(valueWidth), CellStyle.Default);
            }

            if (height > 3)
            {
                string hex = "#" + _Channels[0].ToString("X2", CultureInfo.InvariantCulture)
                    + _Channels[1].ToString("X2", CultureInfo.InvariantCulture)
                    + _Channels[2].ToString("X2", CultureInfo.InvariantCulture);
                CellStyle swatch = CellStyle.Default.WithBackground(Value);
                surface.Fill(new Rect(0, 3, width, 1), Cell.Blank(swatch));
                surface.DrawText(0, 3, " " + hex + " ", CellStyle.Default.WithForeground(Color.FromRgb(255, 255, 255)).WithBackground(Value));
            }
        }

        private void Nudge(int delta)
        {
            int next = _Channels[_Active] + delta;
            if (next < 0)
                next = 0;
            if (next > 255)
                next = 255;

            _Channels[_Active] = next;
        }
    }
}

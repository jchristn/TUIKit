namespace TUIKit.Example
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Ascii;
    using TUIKit.Input;
    using TUIKit.Widgets;

    /// <summary>
    /// An interactive tour demo that renders a sample word in one ASCII-art font at a time and lets the
    /// user scroll through the whole font library with the Left and Right arrow keys (Home and End jump
    /// to the ends). It is focusable, so the guided tour hands it keys first; it consumes only the
    /// arrow keys and lets everything else fall through to the tour's own navigation.
    /// </summary>
    internal sealed class FontGallery : IWidget, IFocusable
    {
        private readonly List<IAsciiFont> _Fonts;
        private readonly string _Sample;
        private readonly int _MaxHeight;
        private int _Index;

        internal FontGallery(AsciiFontLibrary library, string sample)
        {
            if (library == null)
                throw new ArgumentNullException(nameof(library));

            _Sample = sample ?? throw new ArgumentNullException(nameof(sample));
            _Fonts = new List<IAsciiFont>(library.Enumerate());
            _Fonts.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

            int max = 1;
            for (int i = 0; i < _Fonts.Count; i++)
            {
                if (_Fonts[i].Metrics.Height > max)
                    max = _Fonts[i].Metrics.Height;
            }

            _MaxHeight = max;
        }

        public bool HandleKey(KeyEvent key)
        {
            if (_Fonts.Count == 0)
                return false;

            switch (key.Code)
            {
                case KeyCode.Right:
                    _Index = (_Index + 1) % _Fonts.Count;
                    return true;
                case KeyCode.Left:
                    _Index = (_Index - 1 + _Fonts.Count) % _Fonts.Count;
                    return true;
                case KeyCode.Home:
                    _Index = 0;
                    return true;
                case KeyCode.End:
                    _Index = _Fonts.Count - 1;
                    return true;
                default:
                    return false;
            }
        }

        public Size Measure(Size available)
        {
            return new Size(available.Width, Math.Min(available.Height, _MaxHeight + 1));
        }

        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));
            if (_Fonts.Count == 0)
                return;

            IAsciiFont font = _Fonts[_Index];
            CellStyle header = CellStyle.Default.WithForeground(Color.FromPalette(3));
            string label = font.Name + "   (" + (_Index + 1) + "/" + _Fonts.Count + ")   ← → to scroll";
            surface.DrawText(0, 0, label, header);

            IReadOnlyList<string> rows = AsciiArt.Render(_Sample, font);
            CellStyle ink = CellStyle.Default.WithForeground(Color.FromPalette(6));
            for (int r = 0; r < rows.Count && r + 1 < surface.Size.Height; r++)
                surface.DrawText(0, r + 1, rows[r], ink);
        }
    }
}

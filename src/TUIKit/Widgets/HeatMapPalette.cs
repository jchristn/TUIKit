namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using TUIKit;

    /// <summary>
    /// An ordered ramp of colors, from low to high intensity, that a <see cref="HeatMap"/> or
    /// <see cref="CalendarHeatMap"/> draws its cells in, plus a separate color for the empty (zero) level. Build one from
    /// explicit colors or use a preset (<see cref="Green"/>, <see cref="Blue"/>, <see cref="Purple"/>,
    /// <see cref="Heat"/>, <see cref="Grayscale"/>).
    /// </summary>
    /// <remarks>
    /// <see cref="ColorAt"/> interpolates between neighboring stops in RGB when both are truecolor
    /// (<see cref="ColorKind.Rgb"/>); otherwise it picks the nearer stop, because palette indexes and the default color
    /// have no RGB value to blend. The palette does not know the terminal's color depth: as with every
    /// <see cref="Color"/>, the renderer quantizes truecolor to 256 or 16 colors at emit time
    /// (<see cref="Terminal.ColorQuantizer"/>), and drops color entirely when the terminal has none. Adjacent stops can
    /// then merge, which is why heat maps keep a distinct glyph per level. The preset colors sit in the middle of the
    /// lightness range so they read on dark and light backgrounds, and each preset's stops map to distinct 256-color
    /// indexes. Instances are immutable and safe to share across threads and widgets.
    /// </remarks>
    public sealed class HeatMapPalette
    {
        #region Private-Members

        private static readonly Color _DefaultEmpty = Color.FromRgb(0x8C8C8C);

        private readonly Color[] _Colors;
        private readonly ReadOnlyCollection<Color> _ColorsView;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new palette named <c>"Custom"</c> with a neutral gray empty color.
        /// </summary>
        /// <param name="colors">The colors from low to high intensity. Must not be null; at least two. Copied.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="colors"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when fewer than two colors are supplied.</exception>
        public HeatMapPalette(params Color[] colors)
            : this("Custom", _DefaultEmpty, colors)
        {
        }

        /// <summary>
        /// Initializes a new palette.
        /// </summary>
        /// <param name="name">A display name, such as <c>"Ocean"</c>. Must not be null.</param>
        /// <param name="emptyColor">The color of the zero level (see <see cref="EmptyColor"/>).</param>
        /// <param name="colors">The colors from low to high intensity. Must not be null; at least two. Copied.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> or <paramref name="colors"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when fewer than two colors are supplied.</exception>
        public HeatMapPalette(string name, Color emptyColor, IEnumerable<Color> colors)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (colors == null)
                throw new ArgumentNullException(nameof(colors));

            Color[] copy = colors.ToArray();
            if (copy.Length < 2)
                throw new ArgumentException("A heat map palette needs at least two colors.", nameof(colors));

            Name = name;
            EmptyColor = emptyColor;
            _Colors = copy;
            _ColorsView = new ReadOnlyCollection<Color>(_Colors);
        }

        /// <summary>
        /// Gets the green preset, close to the default <see cref="HeatMap.CellColor"/> look.
        /// </summary>
        public static HeatMapPalette Green { get; } = new HeatMapPalette(
            "Green", _DefaultEmpty, new[] { Color.FromRgb(0x8FD19E), Color.FromRgb(0x52B56A), Color.FromRgb(0x2E9446), Color.FromRgb(0x1E6E31) });

        /// <summary>
        /// Gets the blue preset.
        /// </summary>
        public static HeatMapPalette Blue { get; } = new HeatMapPalette(
            "Blue", _DefaultEmpty, new[] { Color.FromRgb(0x9CC7F0), Color.FromRgb(0x5A9FE0), Color.FromRgb(0x2F74C0), Color.FromRgb(0x1F4E96) });

        /// <summary>
        /// Gets the purple preset.
        /// </summary>
        public static HeatMapPalette Purple { get; } = new HeatMapPalette(
            "Purple", _DefaultEmpty, new[] { Color.FromRgb(0xC9B3F0), Color.FromRgb(0x9F7FDC), Color.FromRgb(0x7A52C2), Color.FromRgb(0x56349A) });

        /// <summary>
        /// Gets the warm "heat" preset, from yellow through orange to red.
        /// </summary>
        public static HeatMapPalette Heat { get; } = new HeatMapPalette(
            "Heat", _DefaultEmpty, new[] { Color.FromRgb(0xF2D24B), Color.FromRgb(0xF29B30), Color.FromRgb(0xE0582A), Color.FromRgb(0xB8242A) });

        /// <summary>
        /// Gets the grayscale preset, from light to dark gray. Its empty color is a lighter gray than its first stop.
        /// </summary>
        public static HeatMapPalette Grayscale { get; } = new HeatMapPalette(
            "Grayscale", Color.FromRgb(0xB4B4B4), new[] { Color.FromRgb(0x9E9E9E), Color.FromRgb(0x7A7A7A), Color.FromRgb(0x585858), Color.FromRgb(0x3A3A3A) });

        /// <summary>
        /// Gets the presets in a fixed order: <see cref="Green"/>, <see cref="Blue"/>, <see cref="Purple"/>,
        /// <see cref="Heat"/>, <see cref="Grayscale"/>. Useful for cycling through them.
        /// </summary>
        public static IReadOnlyList<HeatMapPalette> Presets { get; } = new ReadOnlyCollection<HeatMapPalette>(new[] { Green, Blue, Purple, Heat, Grayscale });

        #endregion

        #region Public-Members

        /// <summary>
        /// Gets the display name. Never null.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the color of the zero level when a map uses discrete levels (<see cref="HeatMap.LevelCount"/>), so empty
        /// cells read differently from the lowest non-zero level. The presets use a neutral gray.
        /// </summary>
        public Color EmptyColor { get; }

        /// <summary>
        /// Gets the colors from low to high intensity (at least two). Read-only.
        /// </summary>
        public IReadOnlyList<Color> Colors
        {
            get { return _ColorsView; }
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Gets the color for a normalized intensity: 0 is the first color, 1 the last, and values between blend the two
        /// nearest stops (in RGB when both are truecolor, otherwise the nearer stop). Values outside 0 to 1 are clamped;
        /// NaN is treated as 0.
        /// </summary>
        /// <param name="value">The normalized intensity, 0 to 1.</param>
        /// <returns>The color.</returns>
        public Color ColorAt(double value)
        {
            double t = double.IsNaN(value) ? 0.0 : Math.Max(0.0, Math.Min(1.0, value));
            double position = t * (_Colors.Length - 1);
            int index = (int)Math.Floor(position);
            if (index >= _Colors.Length - 1)
                return _Colors[_Colors.Length - 1];

            double fraction = position - index;
            Color low = _Colors[index];
            Color high = _Colors[index + 1];
            if (fraction <= 0.0)
                return low;
            if (low.Kind != ColorKind.Rgb || high.Kind != ColorKind.Rgb)
                return fraction < 0.5 ? low : high;

            return Color.FromRgb(Blend(low.R, high.R, fraction), Blend(low.G, high.G, fraction), Blend(low.B, high.B, fraction));
        }

        /// <summary>
        /// Gets the color for a discrete level: level 0 is <see cref="EmptyColor"/>, and levels 1 to
        /// <paramref name="levelCount"/> - 1 spread evenly over <see cref="Colors"/>, so the first non-zero level is the
        /// first color and the top level the last.
        /// </summary>
        /// <param name="level">The level, 0 to <paramref name="levelCount"/> - 1.</param>
        /// <param name="levelCount">The number of levels, 2 to 16 (see <see cref="HeatMap.LevelCount"/>).</param>
        /// <returns>The color.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="levelCount"/> is outside 2 to 16 or <paramref name="level"/> is outside 0 to
        /// <paramref name="levelCount"/> - 1.
        /// </exception>
        public Color ColorForLevel(int level, int levelCount)
        {
            if (levelCount < 2 || levelCount > 16)
                throw new ArgumentOutOfRangeException(nameof(levelCount), "levelCount must be 2 to 16.");
            if (level < 0 || level >= levelCount)
                throw new ArgumentOutOfRangeException(nameof(level), "level must be 0 to levelCount - 1.");

            if (level == 0)
                return EmptyColor;
            if (levelCount == 2)
                return _Colors[_Colors.Length - 1];

            return ColorAt((double)(level - 1) / (levelCount - 2));
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return Name;
        }

        #endregion

        #region Private-Methods

        private static byte Blend(byte low, byte high, double fraction)
        {
            double value = low + (high - low) * fraction;
            return (byte)Math.Max(0, Math.Min(255, (int)Math.Round(value, MidpointRounding.AwayFromZero)));
        }

        #endregion
    }
}

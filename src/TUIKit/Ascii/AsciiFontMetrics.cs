namespace TUIKit.Ascii
{
    using System;

    /// <summary>
    /// The immutable layout parameters that describe how a font's glyphs are shaped and packed:
    /// display name, glyph height, baseline, hardblank character, default packing mode, and the
    /// smushing rules. Populated from a FIGlet font header by <see cref="FigletFontLoader"/> or by a
    /// built-in font. Instances are read-only and safe to share across threads.
    /// </summary>
    public sealed class AsciiFontMetrics
    {
        /// <summary>
        /// Gets the human-readable font name (for example the FIGlet header's font name). Never null.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the height of every glyph in rows. Always one or greater.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Gets the baseline row, measured from the top, as reported by the font. Between zero and
        /// <see cref="Height"/>.
        /// </summary>
        public int Baseline { get; }

        /// <summary>
        /// Gets the hardblank character. It occupies a column in glyph data but is replaced by a space
        /// after composition, and it blocks smushing except under <see cref="AsciiSmushRule.HardBlank"/>.
        /// Defaults to <c>'$'</c> for most FIGlet fonts.
        /// </summary>
        public char HardBlank { get; }

        /// <summary>
        /// Gets the packing mode the font prefers when a caller does not override it.
        /// </summary>
        public AsciiLayoutMode DefaultLayout { get; }

        /// <summary>
        /// Gets the horizontal smushing rules used when <see cref="AsciiLayoutMode.Smushing"/> is in
        /// effect. <see cref="AsciiSmushRule.None"/> selects universal smushing.
        /// </summary>
        public AsciiSmushRule SmushRules { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AsciiFontMetrics"/> class.
        /// </summary>
        /// <param name="name">The font name. Must not be null.</param>
        /// <param name="height">The glyph height in rows. Must be one or greater.</param>
        /// <param name="baseline">The baseline row from the top. Clamped into the range 0 to <paramref name="height"/>.</param>
        /// <param name="hardBlank">The hardblank character.</param>
        /// <param name="defaultLayout">The default packing mode.</param>
        /// <param name="smushRules">The horizontal smushing rules.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="height"/> is less than one.</exception>
        public AsciiFontMetrics(
            string name,
            int height,
            int baseline,
            char hardBlank,
            AsciiLayoutMode defaultLayout,
            AsciiSmushRule smushRules)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (height < 1)
                throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be one or greater.");

            Name = name;
            Height = height;
            Baseline = baseline < 0 ? 0 : (baseline > height ? height : baseline);
            HardBlank = hardBlank;
            DefaultLayout = defaultLayout;
            SmushRules = smushRules;
        }
    }
}

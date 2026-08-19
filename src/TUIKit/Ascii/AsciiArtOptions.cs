namespace TUIKit.Ascii
{
    using System;

    /// <summary>
    /// Options controlling how <see cref="AsciiArt.Render(string, IAsciiFont, AsciiArtOptions)"/>
    /// composes text: the packing mode, alignment within an optional target width, and whether to
    /// trim uniform blank columns from the result. All properties have sensible defaults, so passing
    /// no options renders left-aligned art in the font's preferred layout.
    /// </summary>
    public sealed class AsciiArtOptions
    {
        private int _MaxWidth;

        /// <summary>
        /// Gets or sets the packing mode, or null to use the font's
        /// <see cref="AsciiFontMetrics.DefaultLayout"/>. Defaults to null.
        /// </summary>
        public AsciiLayoutMode? Layout { get; set; }

        /// <summary>
        /// Gets or sets the horizontal alignment applied when <see cref="MaxWidth"/> is greater than
        /// the composed width. Defaults to <see cref="AsciiArtAlignment.Left"/>.
        /// </summary>
        public AsciiArtAlignment Alignment { get; set; } = AsciiArtAlignment.Left;

        /// <summary>
        /// Gets or sets the target width in columns. When zero (the default) the result is exactly as
        /// wide as the art. When greater than the art, rows are padded per <see cref="Alignment"/>.
        /// Minimum zero.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a negative value.</exception>
        public int MaxWidth
        {
            get { return _MaxWidth; }
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "MaxWidth must be zero or greater.");

                _MaxWidth = value;
            }
        }

        /// <summary>
        /// Gets or sets whether leading and trailing columns that are blank in every row are trimmed
        /// from the composed art before alignment. Defaults to <c>true</c>.
        /// </summary>
        public bool TrimBlankColumns { get; set; } = true;
    }
}

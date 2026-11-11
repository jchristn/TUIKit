namespace TUIKit.Widgets
{
    /// <summary>
    /// How a <see cref="HeatMapPalette"/> colors heat map cells. Used by <see cref="HeatMap.PaletteMode"/> and
    /// <see cref="CalendarHeatMap.PaletteMode"/>.
    /// </summary>
    public enum HeatMapPaletteMode
    {
        /// <summary>
        /// The palette color is the glyph foreground; the cell background is left at its default. This is the default,
        /// and the level glyphs stay visible at every color depth.
        /// </summary>
        Foreground = 0,

        /// <summary>
        /// The palette color fills the cell (foreground and background), drawing solid tiles. The zero level of a
        /// levelled map keeps only its foreground so empty cells stay distinct. With color off the backgrounds drop away
        /// and the level glyphs show in the default color, so levels remain readable.
        /// </summary>
        Background = 1
    }
}

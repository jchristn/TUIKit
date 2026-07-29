namespace TUIKit.Widgets
{
    /// <summary>
    /// The border style drawn around and between a <see cref="Table"/>'s cells.
    /// </summary>
    public enum TableBorder
    {
        /// <summary>No border; columns are laid out without frame glyphs. The default.</summary>
        None = 0,

        /// <summary>A square box-drawing border (┌ ─ ┐ │ └ ┘ ├ ┼ ┤).</summary>
        Square = 1,

        /// <summary>A rounded box-drawing border (╭ ─ ╮ │ ╰ ╯ ├ ┼ ┤).</summary>
        Rounded = 2
    }
}

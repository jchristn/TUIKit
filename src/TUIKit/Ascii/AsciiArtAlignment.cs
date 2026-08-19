namespace TUIKit.Ascii
{
    /// <summary>
    /// Horizontal alignment of composed ASCII art rows within a target width. Only meaningful when a
    /// width wider than the art is supplied (for example <see cref="AsciiArtOptions.MaxWidth"/> or an
    /// <see cref="TUIKit.Widgets.AsciiArtText"/> surface wider than the banner).
    /// </summary>
    public enum AsciiArtAlignment
    {
        /// <summary>Rows start at the left edge. The default.</summary>
        Left = 0,

        /// <summary>Rows are centered, with any odd remainder padded on the right.</summary>
        Center = 1,

        /// <summary>Rows are pushed to the right edge.</summary>
        Right = 2
    }
}

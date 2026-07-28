namespace TUIKit
{
    using System;

    /// <summary>
    /// Text rendering attributes that can be combined on a single cell. Values are bit flags and
    /// may be OR-combined.
    /// </summary>
    [Flags]
    public enum CellAttributes
    {
        /// <summary>
        /// No attributes.
        /// </summary>
        None = 0,

        /// <summary>
        /// Bold or increased-intensity text.
        /// </summary>
        Bold = 1 << 0,

        /// <summary>
        /// Faint or decreased-intensity text.
        /// </summary>
        Dim = 1 << 1,

        /// <summary>
        /// Italic text.
        /// </summary>
        Italic = 1 << 2,

        /// <summary>
        /// Underlined text.
        /// </summary>
        Underline = 1 << 3,

        /// <summary>
        /// Blinking text.
        /// </summary>
        Blink = 1 << 4,

        /// <summary>
        /// Reverse video (foreground and background swapped).
        /// </summary>
        Reverse = 1 << 5,

        /// <summary>
        /// Hidden or concealed text.
        /// </summary>
        Hidden = 1 << 6,

        /// <summary>
        /// Struck-through text.
        /// </summary>
        Strikethrough = 1 << 7
    }
}

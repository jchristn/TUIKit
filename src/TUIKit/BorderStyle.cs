namespace TUIKit
{
    /// <summary>
    /// The kind of border drawn around a region or box. <see cref="None"/> draws no border; every
    /// other value draws a single-cell frame. <see cref="Ascii"/> uses only ASCII characters for
    /// terminals or fonts without box-drawing glyphs; the remaining values use Unicode (ANSI/VT)
    /// box-drawing glyphs in different weights and corner shapes.
    /// </summary>
    public enum BorderStyle
    {
        /// <summary>No border.</summary>
        None = 0,

        /// <summary>ASCII border using <c>+</c>, <c>-</c>, and <c>|</c>. The safest fallback.</summary>
        Ascii = 1,

        /// <summary>Single-line box-drawing border (<c>┌ ─ ┐ │ └ ┘</c>).</summary>
        Line = 2,

        /// <summary>Single-line border with rounded corners (<c>╭ ╮ ╰ ╯</c>).</summary>
        Rounded = 3,

        /// <summary>Double-line box-drawing border (<c>╔ ═ ╗ ║ ╚ ╝</c>).</summary>
        Double = 4,

        /// <summary>Heavy/thick box-drawing border (<c>┏ ━ ┓ ┃ ┗ ┛</c>).</summary>
        Thick = 5
    }
}

namespace TUIKit.Ascii
{
    using System;

    /// <summary>
    /// The horizontal smushing rules a font may enable, matching the six FIGlet controlled-smushing
    /// rules. The values are bit flags and correspond to the low six bits of a FIGlet layout mode, so
    /// a font header's smush value maps directly onto this enum. Applied only when
    /// <see cref="AsciiLayoutMode.Smushing"/> is in effect; when no rule matches a touching pair the
    /// characters are packed by kerning instead. When no rule is set the engine falls back to
    /// universal smushing (the later character wins).
    /// </summary>
    [Flags]
    public enum AsciiSmushRule
    {
        /// <summary>No controlled rule; universal smushing is used.</summary>
        None = 0,

        /// <summary>Two identical characters smush into one (excluding the hardblank). FIGlet bit 1.</summary>
        EqualCharacter = 1,

        /// <summary>
        /// An underscore is replaced by any of <c>| / \ [ ] { } ( ) &lt; &gt;</c> that it touches.
        /// FIGlet bit 2.
        /// </summary>
        Underscore = 2,

        /// <summary>
        /// A class hierarchy (<c>| &lt; / \ &lt; [ ] &lt; { } &lt; ( ) &lt; &lt; &gt;</c>) lets the
        /// higher class replace the lower. FIGlet bit 4.
        /// </summary>
        Hierarchy = 4,

        /// <summary>Opposite brackets (<c>[]</c>, <c>{}</c>, <c>()</c>) smush into a vertical bar. FIGlet bit 8.</summary>
        OppositePair = 8,

        /// <summary>
        /// <c>/\</c> becomes <c>|</c>, <c>\/</c> becomes <c>Y</c>, and <c>&gt;&lt;</c> becomes
        /// <c>X</c>. FIGlet bit 16.
        /// </summary>
        BigX = 16,

        /// <summary>Two hardblanks smush into one hardblank. FIGlet bit 32.</summary>
        HardBlank = 32
    }
}

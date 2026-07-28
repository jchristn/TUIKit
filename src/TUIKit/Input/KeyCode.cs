namespace TUIKit.Input
{
    /// <summary>
    /// Identifies a non-character key, or <see cref="Character"/> when the event carries a printable
    /// code point.
    /// </summary>
    public enum KeyCode
    {
        /// <summary>A printable character; the code point is carried separately.</summary>
        Character = 0,

        /// <summary>The Enter/Return key.</summary>
        Enter = 1,

        /// <summary>The Escape key.</summary>
        Escape = 2,

        /// <summary>The Tab key.</summary>
        Tab = 3,

        /// <summary>The Backspace key.</summary>
        Backspace = 4,

        /// <summary>The Delete key.</summary>
        Delete = 5,

        /// <summary>The Insert key.</summary>
        Insert = 6,

        /// <summary>The Up arrow.</summary>
        Up = 7,

        /// <summary>The Down arrow.</summary>
        Down = 8,

        /// <summary>The Left arrow.</summary>
        Left = 9,

        /// <summary>The Right arrow.</summary>
        Right = 10,

        /// <summary>The Home key.</summary>
        Home = 11,

        /// <summary>The End key.</summary>
        End = 12,

        /// <summary>The Page Up key.</summary>
        PageUp = 13,

        /// <summary>The Page Down key.</summary>
        PageDown = 14,

        /// <summary>The F1 function key.</summary>
        F1 = 101,

        /// <summary>The F2 function key.</summary>
        F2 = 102,

        /// <summary>The F3 function key.</summary>
        F3 = 103,

        /// <summary>The F4 function key.</summary>
        F4 = 104,

        /// <summary>The F5 function key.</summary>
        F5 = 105,

        /// <summary>The F6 function key.</summary>
        F6 = 106,

        /// <summary>The F7 function key.</summary>
        F7 = 107,

        /// <summary>The F8 function key.</summary>
        F8 = 108,

        /// <summary>The F9 function key.</summary>
        F9 = 109,

        /// <summary>The F10 function key.</summary>
        F10 = 110,

        /// <summary>The F11 function key.</summary>
        F11 = 111,

        /// <summary>The F12 function key.</summary>
        F12 = 112
    }
}

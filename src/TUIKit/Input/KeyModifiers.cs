namespace TUIKit.Input
{
    using System;

    /// <summary>
    /// Modifier keys held during a key event. Values are bit flags and may be OR-combined.
    /// </summary>
    [Flags]
    public enum KeyModifiers
    {
        /// <summary>No modifiers.</summary>
        None = 0,

        /// <summary>The Shift key.</summary>
        Shift = 1 << 0,

        /// <summary>The Alt (Meta) key.</summary>
        Alt = 1 << 1,

        /// <summary>The Control key.</summary>
        Ctrl = 1 << 2,

        /// <summary>The Super (Windows/Command) key.</summary>
        Super = 1 << 3
    }
}

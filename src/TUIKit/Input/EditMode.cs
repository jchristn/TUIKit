namespace TUIKit.Input
{
    /// <summary>
    /// A modal-editing mode in the spirit of vi: keys mean different things depending on the mode.
    /// </summary>
    public enum EditMode
    {
        /// <summary>Keys invoke commands and motions rather than inserting text.</summary>
        Normal = 0,

        /// <summary>Keys insert text.</summary>
        Insert = 1,

        /// <summary>Keys extend a selection.</summary>
        Visual = 2,

        /// <summary>Keys build a command line.</summary>
        Command = 3
    }
}

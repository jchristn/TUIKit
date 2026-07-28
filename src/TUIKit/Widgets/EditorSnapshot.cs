namespace TUIKit.Widgets
{
    using System.Collections.Generic;

    /// <summary>
    /// An immutable snapshot of editor content and caret position, used for undo and redo.
    /// </summary>
    internal sealed class EditorSnapshot
    {
        internal List<string> Lines { get; }

        internal int Row { get; }

        internal int Column { get; }

        internal EditorSnapshot(List<string> lines, int row, int column)
        {
            Lines = new List<string>(lines);
            Row = row;
            Column = column;
        }
    }
}

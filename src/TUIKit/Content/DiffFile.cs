namespace TUIKit.Content
{
    /// <summary>
    /// One file in a parsed unified diff: its path and the index of its first header line.
    /// </summary>
    internal sealed class DiffFile
    {
        internal DiffFile(string path, int lineIndex)
        {
            Path = path;
            LineIndex = lineIndex;
        }

        internal string Path { get; set; }

        internal int LineIndex { get; }

        internal bool HasHeaderPaths { get; set; }

        internal string? OldPath { get; set; }
    }
}

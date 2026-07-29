namespace TUIKit.Widgets
{
    /// <summary>
    /// One row in a <see cref="FileBrowser"/>: a display name, its full path, and whether it is a
    /// directory or the parent link.
    /// </summary>
    internal sealed class FileEntry
    {
        internal string Name { get; }

        internal string FullPath { get; }

        internal bool IsDirectory { get; }

        internal bool IsParent { get; }

        internal FileEntry(string name, string fullPath, bool isDirectory, bool isParent)
        {
            Name = name;
            FullPath = fullPath;
            IsDirectory = isDirectory;
            IsParent = isParent;
        }
    }
}

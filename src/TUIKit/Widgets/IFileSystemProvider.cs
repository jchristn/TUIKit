namespace TUIKit.Widgets
{
    using System.Collections.Generic;

    /// <summary>
    /// Supplies the file-system facts a hierarchical selector needs — roots, lazy children, the
    /// directory flag, a display name, path normalization, and a path comparer — behind one seam so the
    /// same widget can run over the real disk in production and over an in-memory tree in tests. All
    /// members are expected to be side-effect free and tolerant of unreadable paths (returning an empty
    /// child set rather than throwing). Implementations need not be thread-safe.
    /// </summary>
    public interface IFileSystemProvider
    {
        /// <summary>
        /// Gets the comparer used to key path state, so regenerated path strings compare equal. On
        /// Windows this is case-insensitive; elsewhere it is ordinal.
        /// </summary>
        IEqualityComparer<string> PathComparer { get; }

        /// <summary>
        /// Returns the forest roots (for example the machine's ready drives). Never null or empty.
        /// </summary>
        /// <returns>The root paths.</returns>
        IReadOnlyList<string> GetRoots();

        /// <summary>
        /// Returns the children of a path — directories first, then files when requested — tolerating an
        /// unreadable path by returning an empty set. Never null.
        /// </summary>
        /// <param name="path">The parent path.</param>
        /// <param name="includeFiles">When <c>true</c>, files are listed after directories.</param>
        /// <param name="includeHidden">When <c>true</c>, hidden entries are included.</param>
        /// <returns>The child paths.</returns>
        IReadOnlyList<string> GetChildren(string path, bool includeFiles, bool includeHidden);

        /// <summary>
        /// Reports whether a path is a directory, without enumerating it.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns><c>true</c> when the path is a directory; otherwise <c>false</c>.</returns>
        bool IsDirectory(string path);

        /// <summary>
        /// Returns the short display label for a path (a drive label or file name), never null.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>The display label.</returns>
        string DisplayName(string path);

        /// <summary>
        /// Normalizes a path so a pre-seed key matches an enumerated node (for example resolving to a full
        /// path and trimming a trailing separator). Never null.
        /// </summary>
        /// <param name="path">The path to normalize.</param>
        /// <returns>The normalized path.</returns>
        string Normalize(string path);
    }
}

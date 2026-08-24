namespace TUIKit.Modals
{
    using System;

    /// <summary>
    /// A single excluded path within a <see cref="FileSelection"/>: the absolute path of a hole carved
    /// out of an included subtree, together with whether that path is a directory. This is a named type,
    /// used instead of a tuple, so the "path plus is-directory" pair stays self-describing through the
    /// whole result chain. Instances are immutable value types and safe to share across threads.
    /// </summary>
    public readonly struct FileExclusion : IEquatable<FileExclusion>
    {
        private readonly string _Path;
        private readonly bool _IsDirectory;

        /// <summary>
        /// Initializes a new instance of the <see cref="FileExclusion"/> struct.
        /// </summary>
        /// <param name="path">The absolute path of the excluded node. Must not be null or empty.</param>
        /// <param name="isDirectory"><c>true</c> when the path is a directory; otherwise <c>false</c>.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null or empty.</exception>
        public FileExclusion(string path, bool isDirectory)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("A path is required.", nameof(path));

            _Path = path;
            _IsDirectory = isDirectory;
        }

        /// <summary>
        /// Gets the absolute path of the excluded node. Never null or empty.
        /// </summary>
        public string Path
        {
            get { return _Path; }
        }

        /// <summary>
        /// Gets a value indicating whether the excluded path is a directory.
        /// </summary>
        public bool IsDirectory
        {
            get { return _IsDirectory; }
        }

        /// <inheritdoc/>
        public bool Equals(FileExclusion other)
        {
            return string.Equals(_Path, other._Path, StringComparison.Ordinal) && _IsDirectory == other._IsDirectory;
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is FileExclusion other && Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = _Path != null ? StringComparer.Ordinal.GetHashCode(_Path) : 0;
                hash = (hash * 31) ^ (_IsDirectory ? 1 : 0);
                return hash;
            }
        }
    }
}

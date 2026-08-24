namespace TUIKit.Modals
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The result of a <see cref="FileSelectModal"/>: the top-most included paths plus the holes carved
    /// out of them. This maps cleanly onto a backup-style policy — include each folder, exclude each
    /// hole — and stays correct as files are added under an included folder later. This is a named type,
    /// used instead of a tuple, so the "includes plus excludes" pair stays self-describing. Not
    /// thread-safe; the lists are snapshots taken when the modal finished.
    /// </summary>
    public sealed class FileSelection
    {
        private readonly List<string> _Includes = new List<string>();
        private readonly List<FileExclusion> _Excludes = new List<FileExclusion>();

        /// <summary>
        /// Initializes a new instance of the <see cref="FileSelection"/> class.
        /// </summary>
        /// <param name="includes">The top-most included absolute paths. Must not be null.</param>
        /// <param name="excludes">The excluded holes beneath those includes. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="includes"/> or <paramref name="excludes"/> is null.</exception>
        public FileSelection(IEnumerable<string> includes, IEnumerable<FileExclusion> excludes)
        {
            if (includes == null)
                throw new ArgumentNullException(nameof(includes));
            if (excludes == null)
                throw new ArgumentNullException(nameof(excludes));

            _Includes.AddRange(includes);
            _Excludes.AddRange(excludes);
        }

        /// <summary>
        /// Gets the top-most included absolute paths — the root of each checked subtree. Never null; may
        /// be empty when nothing was checked.
        /// </summary>
        public IReadOnlyList<string> Includes
        {
            get { return _Includes; }
        }

        /// <summary>
        /// Gets the excluded holes beneath the includes, each with its directory-or-file flag. Never null;
        /// may be empty.
        /// </summary>
        public IReadOnlyList<FileExclusion> Excludes
        {
            get { return _Excludes; }
        }
    }
}

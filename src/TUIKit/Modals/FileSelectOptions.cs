namespace TUIKit.Modals
{
    using System.Collections.Generic;

    /// <summary>
    /// Behavior for <see cref="FileSelectModal"/>: the forest roots, the paths to pre-check on open, and
    /// the listing switches. Every property is optional; sensible defaults are applied when a value is
    /// null. Not thread-safe; configure before opening the modal.
    /// </summary>
    public sealed class FileSelectOptions
    {
        /// <summary>
        /// Gets or sets the forest roots. Defaults to the provider's roots (the machine's ready drives on
        /// Windows, <c>/</c> elsewhere) when null.
        /// </summary>
        public IReadOnlyList<string>? Roots { get; set; }

        /// <summary>
        /// Gets or sets the include paths to pre-check and reveal on open. A path that no longer exists is
        /// skipped. Defaults to null (none).
        /// </summary>
        public IReadOnlyList<string>? PreCheckedIncludes { get; set; }

        /// <summary>
        /// Gets or sets the hole paths (unchecked under an include) to pre-check and reveal on open.
        /// Defaults to null (none).
        /// </summary>
        public IReadOnlyList<string>? PreCheckedExcludes { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether files (not just folders) are listed. Defaults to <c>true</c>.
        /// </summary>
        public bool ShowFiles { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether hidden entries are listed. Defaults to <c>true</c>.
        /// </summary>
        public bool ShowHidden { get; set; } = true;

        /// <summary>
        /// Gets or sets the dialog title, or null for none. Defaults to null.
        /// </summary>
        public string? Title { get; set; }
    }
}

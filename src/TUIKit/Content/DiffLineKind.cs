namespace TUIKit.Content
{
    /// <summary>
    /// The role of a line in a rendered diff. A diff computed from two texts uses only <see cref="Context"/>,
    /// <see cref="Added"/>, and <see cref="Removed"/>; the other kinds come from
    /// <see cref="Widgets.DiffView.FromUnifiedDiff"/>.
    /// </summary>
    public enum DiffLineKind
    {
        /// <summary>Unchanged context, present in both sides.</summary>
        Context = 0,

        /// <summary>A line added on the new side.</summary>
        Added = 1,

        /// <summary>A line removed from the old side.</summary>
        Removed = 2,

        /// <summary>A file header: <c>diff --git</c>, <c>---</c>, or <c>+++</c>.</summary>
        FileHeader = 3,

        /// <summary>A hunk header: <c>@@ -a,b +c,d @@</c> with optional context.</summary>
        HunkHeader = 4,

        /// <summary>Metadata: <c>index</c>, file modes, renames, similarity, binary notices, and "no newline at end of file".</summary>
        Meta = 5
    }
}

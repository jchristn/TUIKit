namespace TUIKit.Content
{
    using System.Collections.Generic;

    internal sealed class MarkdownTableBlock : MarkdownBlock
    {
        internal IReadOnlyList<string> Header { get; }

        internal IReadOnlyList<MarkdownColumnAlignment> Alignments { get; }

        // Every row holds exactly Header.Count cells; the parser pads short rows and trims long ones.
        internal IReadOnlyList<IReadOnlyList<string>> Rows { get; }

        internal MarkdownTableBlock(IReadOnlyList<string> header, IReadOnlyList<MarkdownColumnAlignment> alignments, IReadOnlyList<IReadOnlyList<string>> rows)
        {
            Header = header;
            Alignments = alignments;
            Rows = rows;
        }
    }
}

namespace TUIKit.Content
{
    using System.Collections.Generic;

    internal sealed class MarkdownListBlock : MarkdownBlock
    {
        internal bool Ordered { get; }

        internal int Start { get; }

        // The ordered-list delimiter ('.' or ')'); unused for bullet lists.
        internal char Delimiter { get; }

        internal bool Loose { get; }

        internal IReadOnlyList<MarkdownListItem> Items { get; }

        internal MarkdownListBlock(bool ordered, int start, char delimiter, bool loose, IReadOnlyList<MarkdownListItem> items)
        {
            Ordered = ordered;
            Start = start;
            Delimiter = delimiter;
            Loose = loose;
            Items = items;
        }
    }
}

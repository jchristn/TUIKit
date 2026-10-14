namespace TUIKit.Content
{
    using System.Collections.Generic;

    internal sealed class MarkdownQuoteBlock : MarkdownBlock
    {
        internal IReadOnlyList<MarkdownBlock> Children { get; }

        internal MarkdownQuoteBlock(IReadOnlyList<MarkdownBlock> children)
        {
            Children = children;
        }
    }
}

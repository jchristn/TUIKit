namespace TUIKit.Content
{
    using System.Collections.Generic;

    internal sealed class MarkdownDocument
    {
        internal IReadOnlyList<MarkdownBlock> Blocks { get; }

        internal MarkdownDocument(IReadOnlyList<MarkdownBlock> blocks)
        {
            Blocks = blocks;
        }
    }
}

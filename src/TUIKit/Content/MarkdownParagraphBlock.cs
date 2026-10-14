namespace TUIKit.Content
{
    using System.Collections.Generic;

    internal sealed class MarkdownParagraphBlock : MarkdownBlock
    {
        // One entry per hard-break segment; soft-wrapped source lines are already joined with a space.
        internal IReadOnlyList<string> Segments { get; }

        internal MarkdownParagraphBlock(IReadOnlyList<string> segments)
        {
            Segments = segments;
        }
    }
}

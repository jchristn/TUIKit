namespace TUIKit.Content
{
    using System.Collections.Generic;

    internal sealed class MarkdownListItem
    {
        internal bool IsTask { get; }

        internal bool IsDone { get; }

        internal IReadOnlyList<MarkdownBlock> Children { get; }

        internal MarkdownListItem(bool isTask, bool isDone, IReadOnlyList<MarkdownBlock> children)
        {
            IsTask = isTask;
            IsDone = isDone;
            Children = children;
        }
    }
}

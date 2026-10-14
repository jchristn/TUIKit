namespace TUIKit.Content
{
    internal sealed class MarkdownListMarker
    {
        internal bool Ordered { get; }

        internal int Number { get; }

        // The bullet character for bullet lists, or '.' / ')' for ordered lists.
        internal char Delimiter { get; }

        // The column where the item's content starts; continuation lines indented this far belong to the item.
        internal int ContentColumn { get; }

        internal string Content { get; }

        internal MarkdownListMarker(bool ordered, int number, char delimiter, int contentColumn, string content)
        {
            Ordered = ordered;
            Number = number;
            Delimiter = delimiter;
            ContentColumn = contentColumn;
            Content = content;
        }

        internal bool IsSameType(MarkdownListMarker other)
        {
            return other != null && other.Ordered == Ordered && other.Delimiter == Delimiter;
        }
    }
}

namespace TUIKit.Content
{
    internal sealed class MarkdownHeadingBlock : MarkdownBlock
    {
        internal int Level { get; }

        internal string Text { get; }

        internal MarkdownHeadingBlock(int level, string text)
        {
            Level = level;
            Text = text;
        }
    }
}

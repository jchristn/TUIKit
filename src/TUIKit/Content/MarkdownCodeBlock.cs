namespace TUIKit.Content
{
    using System.Collections.Generic;

    internal sealed class MarkdownCodeBlock : MarkdownBlock
    {
        internal string Language { get; }

        internal IReadOnlyList<string> Lines { get; }

        internal MarkdownCodeBlock(string language, IReadOnlyList<string> lines)
        {
            Language = language;
            Lines = lines;
        }
    }
}

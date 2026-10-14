namespace TUIKit.Example
{
    using System;

    // One message in the tour's cached message list: who wrote it and its Markdown body.
    internal sealed class ChatMessage
    {
        internal ChatMessage(string author, string markdown)
        {
            Author = author ?? throw new ArgumentNullException(nameof(author));
            Markdown = markdown ?? throw new ArgumentNullException(nameof(markdown));
        }

        internal string Author { get; }

        internal string Markdown { get; }
    }
}
